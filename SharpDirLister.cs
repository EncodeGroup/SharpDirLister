using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.IO.Compression;
using System.Collections.Generic;
using static SharpDirLister.Interop;

namespace SharpDirLister
{
	public static class FILETIMEExtensions
	{
		public static DateTime ToDateTime(this System.Runtime.InteropServices.ComTypes.FILETIME time)
		{
			ulong high = (ulong)time.dwHighDateTime;
			ulong low = (ulong)time.dwLowDateTime;
			long fileTime = (long)((high << 32) + low);

			return DateTime.FromFileTimeUtc(fileTime);
		}
	}

	public class FileInformation
	{
		public string FullPath;
		public DateTime LastWriteTime;
		public long Size;
	    public static string type = "F";

		public override string ToString()
		{
			return string.Format("{0} | {1} | {2} | {3}", FullPath, LastWriteTime, Size, type);
		}
	}

	public class DirectoryInformation
	{
		public string FullPath;
		public DateTime LastWriteTime;
	    public static string type = "D";

        public override string ToString()
		{
			return string.Format("{0} | {1} | {2}", FullPath, LastWriteTime, type);
		}
	}

	class List
	{
		static IntPtr INVALID_HANDLE_VALUE = new IntPtr(-1);

        //Code based heavily on https://stackoverflow.com/q/47471744
        static bool FindNextFilePInvokeRecursive(string path, out List<FileInformation> files, out List<DirectoryInformation> directories, Log.Logger logger)
        {
            List<FileInformation> fileList = new List<FileInformation>();
            List<DirectoryInformation> directoryList = new List<DirectoryInformation>();
            IntPtr findHandle = INVALID_HANDLE_VALUE;
            List<Tuple<string, DateTime>> info = new List<Tuple<string, DateTime>>();

            try
            {
                findHandle = FindFirstFileW(path + @"\*", out WIN32_FIND_DATAW findData);

                if (findHandle != INVALID_HANDLE_VALUE)
                {
                    do
                    {
                        if (findData.cFileName != "." && findData.cFileName != "..")
                        {
                            string fullPath = path + @"\" + findData.cFileName;

                            if (findData.dwFileAttributes.HasFlag(FileAttributes.Directory) && !findData.dwFileAttributes.HasFlag(FileAttributes.ReparsePoint))
                            {
                                var dirdata = new DirectoryInformation { FullPath = fullPath, LastWriteTime = findData.ftLastWriteTime.ToDateTime() };
                                directoryList.Add(dirdata);
                                List<FileInformation> subDirectoryFileList = new List<FileInformation>();
                                List<DirectoryInformation> subDirectoryDirectoryList = new List<DirectoryInformation>();

                                if (FindNextFilePInvokeRecursive(fullPath, out subDirectoryFileList, out subDirectoryDirectoryList, logger))
                                {
                                    fileList.AddRange(subDirectoryFileList);
                                    directoryList.AddRange(subDirectoryDirectoryList);
                                }
                            }

                            else if (!findData.dwFileAttributes.HasFlag(FileAttributes.Directory))
                            {
                                var filedata = new FileInformation { FullPath = fullPath, LastWriteTime = findData.ftLastWriteTime.ToDateTime(), Size = (long)findData.nFileSizeLow + (long)findData.nFileSizeHigh * 4294967296 };
                                fileList.Add(filedata);
                            }
                        }
                    } while (FindNextFile(findHandle, out findData));
                }
            }

            catch (Exception exception)
            {
                Console.WriteLine(exception.ToString());

                if (findHandle != INVALID_HANDLE_VALUE)
                {
                    FindClose(findHandle);
                }

                files = null;
                directories = null;
                return false;
            }

            if (findHandle != INVALID_HANDLE_VALUE)
            {
                FindClose(findHandle);
            }

            files = fileList;
            directories = directoryList;
            return true;
        }

        static bool FindNextFilePInvokeRecursiveParalleled(string path, out List<FileInformation> files, out List<DirectoryInformation> directories, Log.Logger logger)
        {
            List<FileInformation> fileList = new List<FileInformation>();
            object fileListLock = new object();
            List<DirectoryInformation> directoryList = new List<DirectoryInformation>();
            object directoryListLock = new object();
            IntPtr findHandle = INVALID_HANDLE_VALUE;
            List<Tuple<string, DateTime>> info = new List<Tuple<string, DateTime>>();

            try
            {
                path = path.EndsWith(@"\") ? path : path + @"\";
                findHandle = FindFirstFileW(path + @"*", out WIN32_FIND_DATAW findData);

                if (findHandle != INVALID_HANDLE_VALUE)
                {
                    do
                    {
                        if (findData.cFileName != "." && findData.cFileName != "..")
                        {
                            string fullPath = path + findData.cFileName;

                            if (findData.dwFileAttributes.HasFlag(FileAttributes.Directory) && !findData.dwFileAttributes.HasFlag(FileAttributes.ReparsePoint))
                            {
                                var dirdata = new DirectoryInformation { FullPath = fullPath, LastWriteTime = findData.ftLastWriteTime.ToDateTime() };
                                directoryList.Add(dirdata);
                            }

                            else if (!findData.dwFileAttributes.HasFlag(FileAttributes.Directory))
                            {
                                var filedata = new FileInformation { FullPath = fullPath, LastWriteTime = findData.ftLastWriteTime.ToDateTime() };
                                fileList.Add(filedata);
                            }
                        }
                    } while (FindNextFile(findHandle, out findData));

                    directoryList.AsParallel().ForAll(x =>
                    {
                        List<FileInformation> subDirectoryFileList = new List<FileInformation>();
                        List<DirectoryInformation> subDirectoryDirectoryList = new List<DirectoryInformation>();

                        if (FindNextFilePInvokeRecursive(x.FullPath, out subDirectoryFileList, out subDirectoryDirectoryList, logger))
                        {
                            lock (fileListLock)
                            {
                                fileList.AddRange(subDirectoryFileList);
                            }

                            lock (directoryListLock)
                            {
                                directoryList.AddRange(subDirectoryDirectoryList);
                            }
                        }
                    });
                }
            }

            catch (Exception exception)
            {
                Console.WriteLine(exception.ToString());

                if (findHandle != INVALID_HANDLE_VALUE)
                {
                    FindClose(findHandle);
                }

                files = null;
                directories = null;
                return false;
            }

            if (findHandle != INVALID_HANDLE_VALUE)
            {
                FindClose(findHandle);
            }

            files = fileList;
            directories = directoryList;
            return true;
        }

        public static void CompressFile(string path)
		{
			FileStream sourceFile = File.OpenRead(path);
			FileStream destinationFile = File.Create(path + ".gz");
			byte[] buffer = new byte[sourceFile.Length];
			sourceFile.Read(buffer, 0, buffer.Length);

			using (GZipStream output = new GZipStream(destinationFile, CompressionMode.Compress))
			{
				Console.WriteLine("Compressing to {0}", destinationFile.Name);
				output.Write(buffer, 0, buffer.Length);
			}

			sourceFile.Close();
			destinationFile.Close();
		}

		public static void Usage()
		{
            Console.WriteLine("Usage: " + AppDomain.CurrentDomain.FriendlyName + " target outputfolder\n" +
            "Examples:\n" +
            "	" + AppDomain.CurrentDomain.FriendlyName + " c:\\path\\to\\directory c:\\outputfolder\n" +
            "	" + AppDomain.CurrentDomain.FriendlyName + " \\\\path\\to\\share .\\\n" +
            "Will output two files with the format MMddTHHmmss_listing.{.txt,.txt.gz} in the output folder.\n"+
            "Remember to clean up after yourself.\n");
            Environment.Exit(1); //Be careful if running without fork & run
		}

		static void Main(string[] args)
		{
			try
			{
				string filename = DateTime.Now.ToString("MMddTHHmmss") + "_listing.txt";
			    List<FileInformation> files1 = new List<FileInformation>();
			    List<DirectoryInformation> directories1 = new List<DirectoryInformation>();

                if (args.Length != 2)
				{
					Usage();
				}

				else
				{
                    if (!Directory.Exists(args[0])) {
                        Console.WriteLine("Error: Directory \"{0}\" not found.\n", args[0]);
                        Usage();
                    }

                    if (!Directory.Exists(args[1]))
                    {
                        Console.WriteLine("Error: Output directory \"{0}\" not found.\n", args[1]);
                        Usage();
                    }

                    Log.Logger logger = new Log.Logger(Path.Combine(args[1], filename));
					while (!FindNextFilePInvokeRecursiveParalleled(args[0], out files1, out directories1, logger))
					{
						Thread.Sleep(1000);
					}

					
				    files1.Sort((a, b) => string.Compare(a.FullPath, b.FullPath));
				    directories1.Sort((a, b) => string.Compare(a.FullPath, b.FullPath));
				    foreach (var filedata in files1)
				    {
				        logger.WriteLine(filedata.ToString());
				    }
				    foreach (var filedata in directories1)
				    {
				        logger.WriteLine(filedata.ToString());
				    }
				    logger.Close();
                    CompressFile(args[1] + "\\" + filename);
					Console.WriteLine("Done!");
				}
			}
			catch (Exception exception)
			{
                //TODO: If I crash I will not output the listing
				Console.WriteLine(exception.Message);
				Console.WriteLine(exception.StackTrace);
			}
		}
	}
}