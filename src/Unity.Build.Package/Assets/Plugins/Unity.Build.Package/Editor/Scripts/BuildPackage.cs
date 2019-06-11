using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.IO;
using UnityEditor.PackageManager;
using System.Text;
using System;

namespace UnityEditor.Build.Package
{
    public class BuildPackage
    {



        [MenuItem("Build/Package")]
        public static void Build()
        {

            foreach (var packageFile in Directory.GetFiles("Assets", "package.json", SearchOption.AllDirectories))
            {

                string str = File.ReadAllText(packageFile, Encoding.UTF8);
                PackageInfo package = JsonUtility.FromJson<PackageInfo>(str);
                if (string.IsNullOrEmpty(package.repoPackagePath))
                {
                    Debug.LogWarningFormat("require 'repoPackagePath', {0}", packageFile);
                    continue;
                }
                string outpath = Path.GetFullPath(Path.Combine(package.repoPackagePath, package.name + "@" + package.version));
                string packageDir = Path.GetDirectoryName(packageFile);


                string[] files = CopyDirectoryIfChanged(packageDir, outpath, (filePath) =>
                   {
                       string fileLower = filePath.ToLower();
                       if (fileLower.EndsWith(".cs") || fileLower.EndsWith(".meta") || fileLower.EndsWith(".asmdef"))
                           return false;

                       return true;
                   });

                foreach (var file in files)
                {
                    string dstMetaFile = file + ".meta";
                    string relativePath = dstMetaFile.Substring(outpath.Length + 1);
                    string srcMetaFile = Path.Combine(packageDir, relativePath);
                    if (File.Exists(srcMetaFile))
                    {
                        CopyFileIfChanged(srcMetaFile, dstMetaFile);
                    }
                }


                foreach (var asmdefFile in Directory.GetFiles(packageDir, "*.asmdef", SearchOption.AllDirectories))
                {
                    AsmdefInfo asmdef = JsonUtility.FromJson<AsmdefInfo>(File.ReadAllText(asmdefFile));
                    if (string.IsNullOrEmpty(asmdef.name))
                        continue;
                    string asmdefDllName = asmdef.name + ".dll";
                    string srcPath = Path.Combine("Library/ScriptAssemblies", asmdefDllName);
                    if (!File.Exists(srcPath))
                        throw new Exception("not found file " + srcPath);

                    string asmdefDir = Path.GetDirectoryName(asmdefFile);
                    if (asmdefDir.Length == packageDir.Length)
                        asmdefDir = "";
                    else
                        asmdefDir = asmdefDir.Substring(packageDir.Length + 1);

                    string settingPath = Path.Combine(outpath, Path.Combine(asmdefDir, asmdef.name + ".asmdef.dll"));
                    string dstPath = Path.Combine(outpath, Path.Combine(asmdefDir, asmdefDllName));


                    if (File.Exists(settingPath))
                        DeleteFile(settingPath);
                    if (File.Exists(settingPath + ".meta"))
                    {
                        ClearFileAttributes(dstPath + ".meta");
                        File.Move(settingPath + ".meta", dstPath + ".meta"); ;
                    }
                    CopyFileIfChanged(srcPath, dstPath);

                    if (!File.Exists(dstPath + ".meta"))
                        Debug.LogWarningFormat("not .meta file, {0}", dstPath + ".meta");
                    if (File.Exists(srcPath + ".mdb"))
                        CopyFileIfChanged(srcPath + ".mdb", dstPath + ".mdb");
                }

                foreach (var dir in Directory.GetDirectories(outpath))
                {
                    string dstMetaFile = dir + ".meta";
                    string relativePath = dstMetaFile.Substring(outpath.Length+1);
                    string srcMetaFile = Path.Combine(packageDir, relativePath);
                    if (File.Exists(srcMetaFile))
                    {
                        CopyFileIfChanged(srcMetaFile, dstMetaFile);
                    }
                }


                Debug.Log("build package " + outpath);
            }

        }


        public static string ReplaceDirectorySeparatorChar(string path)
        {
            char separatorChar = Path.DirectorySeparatorChar;
            if (separatorChar == '/')
                path = path.Replace('\\', separatorChar);
            else
                path = path.Replace('/', separatorChar);

            return path;
        }

        static string[] CopyDirectoryIfChanged(string src, string dst, Func<string, bool> filter = null)
        {
            src = Path.GetFullPath(src);
            dst = Path.GetFullPath(dst);
            List<string> list = new List<string>();


            if (Directory.Exists(dst))
            {
                foreach (var file in Directory.GetFiles(dst, "*", SearchOption.AllDirectories))
                {
                    ClearFileAttributes(file);
                }

                foreach (var dir in Directory.GetDirectories(dst, "*", SearchOption.AllDirectories))
                {
                    if (!Directory.Exists(dir))
                        continue;
                    string relativePath = dir.Substring(dst.Length + 1);
                    bool exist = false;
                    exist = File.Exists(Path.Combine(src, relativePath));

                    if (!exist)
                    {
                        Directory.Delete(dir, true);
                    }
                }

                foreach (var file in Directory.GetFiles(dst, "*", SearchOption.AllDirectories))
                {
                    string relativePath = file.Substring(dst.Length + 1);
                    bool exist = false;
                    exist = File.Exists(Path.Combine(src, relativePath));
                    if (exist && filter != null)
                    {
                        exist = filter(relativePath);
                    }

                    if (!exist)
                    {
                        DeleteFile(file);
                    }
                }
            }

            foreach (var file in Directory.GetFiles(src, "*", SearchOption.AllDirectories))
            {
                string relativePath = file.Substring(src.Length + 1);
                if (filter != null && !filter(relativePath))
                    continue;
                string dstPath = Path.Combine(dst, relativePath);

                CopyFileIfChanged(file, dstPath);
                list.Add(dstPath);
            }
            return list.ToArray();
        }
        static void ClearFileAttributes(string file)
        {
            if (File.Exists(file))
                File.SetAttributes(file, FileAttributes.Normal);
        }
        static void DeleteFile(string file)
        {
            ClearFileAttributes(file);
            File.Delete(file);
        }

        static bool EqualFileContent(string file1, string file2)
        {
            FileInfo fileInfo1 = new FileInfo(file1);
            FileInfo fileInfo2 = new FileInfo(file2);
            bool equal = true;
            if (fileInfo1.Exists && fileInfo2.Exists && fileInfo1.Length == fileInfo2.Length)
            {
                using (var fs1 = new FileStream(file1, FileMode.Open, FileAccess.Read))
                using (var fs2 = new FileStream(file2, FileMode.Open, FileAccess.Read))
                {
                    int buffSize = 1024 * 4;
                    byte[] buff1 = new byte[buffSize];
                    byte[] buff2 = new byte[buffSize];
                    int count1;
                    int count2;
                    while (equal)
                    {
                        count1 = fs1.Read(buff1, 0, buff1.Length);
                        if (count1 == 0)
                            break;
                        count2 = fs2.Read(buff2, 0, buff2.Length);
                        if (count1 != count2)
                        {
                            equal = false;
                            break;
                        }
                        for (int i = 0; i < count1; i++)
                        {
                            if (buff1[i] != buff2[i])
                            {
                                equal = false;
                                break;
                            }
                        }
                    }
                }
            }
            else
            {
                equal = false;
            }
            return equal;
        }

        static bool CopyFileIfChanged(string src, string dst)
        {
            bool changed = false;

            if (!EqualFileContent(src, dst))
            {
                changed = true;
            }
            if (changed)
            {
                string dstDir = Path.GetDirectoryName(dst);
                if (!string.IsNullOrEmpty(dstDir) && !Directory.Exists(dstDir))
                    Directory.CreateDirectory(dstDir);
                if (File.Exists(dst))
                    File.SetAttributes(dst, FileAttributes.Normal);
                File.Copy(src, dst, true);
            }
            return changed;
        }

        class PackageInfo
        {
            public string name;
            public string displayName;
            public string version;
            public string unity;
            public string description;
            public string[] keywords;
            public string category;
            public string repoPackagePath;
        }

        class AsmdefInfo
        {
            public string name;
        }

    }


}