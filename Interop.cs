﻿using System;
using System.IO;
using System.Runtime.InteropServices;

namespace SharpDirLister
{
    public class Interop
    {
		[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
		public struct WIN32_FIND_DATAW
		{
			public FileAttributes dwFileAttributes;
			internal System.Runtime.InteropServices.ComTypes.FILETIME ftCreationTime;
			internal System.Runtime.InteropServices.ComTypes.FILETIME ftLastAccessTime;
			internal System.Runtime.InteropServices.ComTypes.FILETIME ftLastWriteTime;
			public int nFileSizeHigh;
			public int nFileSizeLow;
			public int dwReserved0;
			public int dwReserved1;
			[MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
			public string cFileName;
			[MarshalAs(UnmanagedType.ByValTStr, SizeConst = 14)]
			public string cAlternateFileName;
		}

		[DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
		public static extern IntPtr FindFirstFileW(string lpFileName, out WIN32_FIND_DATAW lpFindFileData);

		[DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
		public static extern bool FindNextFile(IntPtr hFindFile, out WIN32_FIND_DATAW lpFindFileData);

		[DllImport("kernel32.dll")]
        public static extern bool FindClose(IntPtr hFindFile);
	}
}
