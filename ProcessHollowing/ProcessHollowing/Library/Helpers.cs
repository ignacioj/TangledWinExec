﻿using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using ProcessHollowing.Interop;

namespace ProcessHollowing.Library
{
    using NTSTATUS = Int32;
    using SIZE_T = UIntPtr;

    internal class Helpers
    {
        public static IntPtr AllocateReadWriteMemory(
            IntPtr hProcess,
            IntPtr pAllocateBuffer,
            uint nSizeAllocateBuffer)
        {
            NTSTATUS ntstauts;
            SIZE_T nRegionSize = new SIZE_T(nSizeAllocateBuffer);

            ntstauts = NativeMethods.NtAllocateVirtualMemory(
                hProcess,
                ref pAllocateBuffer,
                SIZE_T.Zero,
                ref nRegionSize,
                ALLOCATION_TYPE.COMMIT | ALLOCATION_TYPE.RESERVE,
                MEMORY_PROTECTION.READWRITE);

            if (ntstauts != Win32Consts.STATUS_SUCCESS)
                return IntPtr.Zero;
            else
                return pAllocateBuffer;
        }


        public static IntPtr AllocateReadWriteMemoryEx(
            IntPtr hProcess,
            IntPtr pAllocateBuffer,
            uint nSizeAllocateBuffer)
        {
            NTSTATUS ntstatus;
            SIZE_T regionSize = new SIZE_T(nSizeAllocateBuffer);

            ntstatus = NativeMethods.NtAllocateVirtualMemory(
                hProcess,
                ref pAllocateBuffer,
                new SIZE_T(nSizeAllocateBuffer),
                ref regionSize,
                ALLOCATION_TYPE.COMMIT | ALLOCATION_TYPE.RESERVE,
                MEMORY_PROTECTION.READWRITE);

            if (ntstatus == Win32Consts.STATUS_SUCCESS)
                return pAllocateBuffer;
            else
                return IntPtr.Zero;
        }


        public static void CopyMemory(
            IntPtr pDestination,
            IntPtr pSource,
            int nSize)
        {
            var tmpBytes = new byte[nSize];
            Marshal.Copy(pSource, tmpBytes, 0, nSize);
            Marshal.Copy(tmpBytes, 0, pDestination, nSize);
        }


        public static IntPtr GetImageBaseAddress(
            IntPtr hProcess,
            IntPtr pPeb)
        {
            IntPtr pImageBase;
            IntPtr pReadBuffer;
            int nSizePointer;
            int nOffsetImageBaseAddress;

            if (Environment.Is64BitOperatingSystem)
            {
                if (!NativeMethods.IsWow64Process(
                    hProcess,
                    out bool Wow64Process))
                {
                    return IntPtr.Zero;
                }

                if (Wow64Process)
                {
                    nSizePointer = 4;
                    nOffsetImageBaseAddress = Marshal.OffsetOf(
                        typeof(PEB32_PARTIAL),
                        "ImageBaseAddress").ToInt32();
                }
                else
                {
                    nSizePointer = 8;
                    nOffsetImageBaseAddress = Marshal.OffsetOf(
                        typeof(PEB64_PARTIAL),
                        "ImageBaseAddress").ToInt32();
                }
            }
            else
            {
                nSizePointer = 4;
                nOffsetImageBaseAddress = Marshal.OffsetOf(
                    typeof(PEB32_PARTIAL),
                    "ImageBaseAddress").ToInt32();
            }

            pReadBuffer = ReadMemory(
                hProcess,
                new IntPtr(pPeb.ToInt64() + nOffsetImageBaseAddress),
                (uint)nSizePointer);

            if (pReadBuffer == IntPtr.Zero)
                return IntPtr.Zero;

            if (nSizePointer == 4)
            {
                pImageBase = new IntPtr(Marshal.ReadInt32(pReadBuffer));
            }
            else
            {
                pImageBase = new IntPtr(Marshal.ReadInt64(pReadBuffer));
            }

            Marshal.FreeHGlobal(pReadBuffer);

            return pImageBase;
        }


        public static IntPtr GetPebAddress(IntPtr hProcess)
        {
            if (!GetProcessBasicInformation(
                hProcess,
                out PROCESS_BASIC_INFORMATION pbi))
            {
                return IntPtr.Zero;
            }
            else
            {
                return pbi.PebBaseAddress;
            }
        }


        public static IntPtr GetPebAddressWow64(IntPtr hProcess)
        {
            NTSTATUS ntstatus;
            IntPtr pInfoBuffer = Marshal.AllocHGlobal(IntPtr.Size);
            IntPtr pPeb;

            ntstatus = NativeMethods.NtQueryInformationProcess(
                hProcess,
                PROCESS_INFORMATION_CLASS.ProcessWow64Information,
                pInfoBuffer,
                (uint)IntPtr.Size,
                IntPtr.Zero);

            if (ntstatus != Win32Consts.STATUS_SUCCESS)
                pPeb = IntPtr.Zero;
            else
                pPeb = Marshal.ReadIntPtr(pInfoBuffer);

            Marshal.FreeHGlobal(pInfoBuffer);

            return pPeb;
        }


        public static bool GetProcessBasicInformation(
            IntPtr hProcess,
            out PROCESS_BASIC_INFORMATION pbi)
        {
            NTSTATUS ntstatus;
            var nSizeBuffer = (uint)Marshal.SizeOf(typeof(PROCESS_BASIC_INFORMATION));
            IntPtr pInfoBuffer = Marshal.AllocHGlobal((int)nSizeBuffer);

            ntstatus = NativeMethods.NtQueryInformationProcess(
                hProcess,
                PROCESS_INFORMATION_CLASS.ProcessBasicInformation,
                pInfoBuffer,
                nSizeBuffer,
                IntPtr.Zero);

            if (ntstatus != Win32Consts.STATUS_SUCCESS)
            {
                pbi = new PROCESS_BASIC_INFORMATION();
            }
            else
            {
                pbi = (PROCESS_BASIC_INFORMATION)Marshal.PtrToStructure(
                    pInfoBuffer,
                    typeof(PROCESS_BASIC_INFORMATION));
            }

            Marshal.FreeHGlobal(pInfoBuffer);

            return (ntstatus == Win32Consts.STATUS_SUCCESS);
        }


        public static IntPtr GetProcessParametersAddress(
            IntPtr hProcess,
            IntPtr pPeb)
        {
            IntPtr pProcessParameters;
            IntPtr pReadBuffer;
            int nSizePointer;
            int nOffsetImageBaseAddress;

            if (Environment.Is64BitOperatingSystem)
            {
                if (!NativeMethods.IsWow64Process(
                    hProcess,
                    out bool Wow64Process))
                {
                    return IntPtr.Zero;
                }

                if (Wow64Process)
                {
                    nSizePointer = 4;
                    nOffsetImageBaseAddress = Marshal.OffsetOf(
                        typeof(PEB32_PARTIAL),
                        "ProcessParameters").ToInt32();
                }
                else
                {
                    nSizePointer = 8;
                    nOffsetImageBaseAddress = Marshal.OffsetOf(
                        typeof(PEB64_PARTIAL),
                        "ProcessParameters").ToInt32();
                }
            }
            else
            {
                nSizePointer = 4;
                nOffsetImageBaseAddress = Marshal.OffsetOf(
                    typeof(PEB32_PARTIAL),
                    "ProcessParameters").ToInt32();
            }

            pReadBuffer = ReadMemory(
                hProcess,
                new IntPtr(pPeb.ToInt64() + nOffsetImageBaseAddress),
                (uint)nSizePointer);

            if (pReadBuffer == IntPtr.Zero)
                return IntPtr.Zero;

            if (nSizePointer == 4)
            {
                pProcessParameters = new IntPtr(Marshal.ReadInt32(pReadBuffer));
            }
            else
            {
                pProcessParameters = new IntPtr(Marshal.ReadInt64(pReadBuffer));
            }

            Marshal.FreeHGlobal(pReadBuffer);

            return pProcessParameters;
        }


        public static string GetWin32ErrorMessage(int code, bool isNtStatus)
        {
            int nReturnedLength;
            ProcessModuleCollection modules;
            FormatMessageFlags dwFlags;
            int nSizeMesssage = 256;
            var message = new StringBuilder(nSizeMesssage);
            IntPtr pNtdll = IntPtr.Zero;

            if (isNtStatus)
            {
                modules = Process.GetCurrentProcess().Modules;

                foreach (ProcessModule mod in modules)
                {
                    if (string.Compare(
                        Path.GetFileName(mod.FileName),
                        "ntdll.dll",
                        StringComparison.OrdinalIgnoreCase) == 0)
                    {
                        pNtdll = mod.BaseAddress;
                        break;
                    }
                }

                dwFlags = FormatMessageFlags.FORMAT_MESSAGE_FROM_HMODULE |
                    FormatMessageFlags.FORMAT_MESSAGE_FROM_SYSTEM;
            }
            else
            {
                dwFlags = FormatMessageFlags.FORMAT_MESSAGE_FROM_SYSTEM;
            }

            nReturnedLength = NativeMethods.FormatMessage(
                dwFlags,
                pNtdll,
                code,
                0,
                message,
                nSizeMesssage,
                IntPtr.Zero);

            if (nReturnedLength == 0)
            {
                return string.Format("[ERROR] Code 0x{0}", code.ToString("X8"));
            }
            else
            {
                return string.Format(
                    "[ERROR] Code 0x{0} : {1}",
                    code.ToString("X8"),
                    message.ToString().Trim());
            }
        }


        public static IntPtr ReadMemory(
            IntPtr hProcess,
            IntPtr pReadAddress,
            uint nSizeToRead)
        {
            NTSTATUS ntstatus;
            IntPtr pBuffer = Marshal.AllocHGlobal((int)nSizeToRead);
            ZeroMemory(pBuffer, (int)nSizeToRead);

            ntstatus = NativeMethods.NtReadVirtualMemory(
                    hProcess,
                    pReadAddress,
                    pBuffer,
                    nSizeToRead,
                    IntPtr.Zero);

            if (ntstatus != Win32Consts.STATUS_SUCCESS)
            {
                Marshal.FreeHGlobal(pBuffer);

                return IntPtr.Zero;
            }

            return pBuffer;
        }


        public static string ResolveImageNamePath(string commandLine)
        {
            int returnedLength;
            string fileName;
            string extension;
            string imagePathName;
            string[] arguments = Regex.Split(commandLine.Trim(), @"\s+");
            var resolvedPath = new StringBuilder(Win32Consts.MAX_PATH);
            var regexExtension = new Regex(@".+\.\S+$");
            var regexExe = new Regex(@".+\.exe$");

            for (var idx = 0; idx < arguments.Length; idx++)
            {
                resolvedPath.Append(arguments[idx]);

                try
                {
                    imagePathName = Path.GetFullPath(resolvedPath.ToString().Trim('"'));
                }
                catch
                {
                    return null;
                }

                if (File.Exists(imagePathName) && regexExe.IsMatch(imagePathName))
                    return imagePathName;

                resolvedPath.Append(" ");
            }

            resolvedPath.Clear();
            resolvedPath.Capacity = Win32Consts.MAX_PATH;

            fileName = arguments[0].Trim('"');
            extension = regexExtension.IsMatch(fileName) ? null : ".exe";

            try
            {
                arguments[0] = Path.GetFullPath(fileName);
            }
            catch
            {
                return null;
            }

            if (regexExe.IsMatch(arguments[0]) && File.Exists(arguments[0]))
            {
                return arguments[0];
            }
            else
            {
                returnedLength = NativeMethods.SearchPath(
                    null,
                    fileName,
                    extension,
                    Win32Consts.MAX_PATH,
                    resolvedPath,
                    IntPtr.Zero);

                if (returnedLength == 0)
                    return null;
                else
                    return resolvedPath.ToString();
            }
        }


        public static bool SetImageBaseAddress(
            IntPtr hProcess,
            IntPtr pPeb,
            IntPtr pImageBaseAddress)
        {
            bool status;
            IntPtr pDataBuffer;
            int nOffset;

            if (IntPtr.Size == 4)
            {
                nOffset = Marshal.OffsetOf(
                    typeof(PEB32_PARTIAL),
                    "ImageBaseAddress").ToInt32();
            }
            else
            {
                nOffset = Marshal.OffsetOf(
                    typeof(PEB64_PARTIAL),
                    "ImageBaseAddress").ToInt32();
            }

            pDataBuffer = Marshal.AllocHGlobal(IntPtr.Size);
            Marshal.WriteIntPtr(pDataBuffer, pImageBaseAddress);

            status = WriteMemory(
                hProcess,
                new IntPtr(pPeb.ToInt64() + nOffset),
                pDataBuffer,
                (uint)IntPtr.Size);
            Marshal.FreeHGlobal(pDataBuffer);

            return status;
        }


        public static bool SetProcessParametersAddress(
            IntPtr hProcess,
            IntPtr pPeb,
            IntPtr pProcessParameters)
        {
            bool status;
            IntPtr pDataBuffer;
            int nOffset;

            if (IntPtr.Size == 4)
            {
                nOffset = Marshal.OffsetOf(
                    typeof(PEB32_PARTIAL),
                    "ProcessParameters").ToInt32();
            }
            else
            {
                nOffset = Marshal.OffsetOf(
                    typeof(PEB64_PARTIAL),
                    "ProcessParameters").ToInt32();
            }

            pDataBuffer = Marshal.AllocHGlobal(IntPtr.Size);
            Marshal.WriteIntPtr(pDataBuffer, pProcessParameters);

            status = WriteMemory(
                hProcess,
                new IntPtr(pPeb.ToInt64() + nOffset),
                pDataBuffer,
                (uint)IntPtr.Size);
            Marshal.FreeHGlobal(pDataBuffer);

            return status;
        }


        public static bool UpdateMemoryProtection(
            IntPtr hProcess,
            IntPtr pBaseAddress,
            uint nSizeToUpdate,
            MEMORY_PROTECTION newProtection)
        {
            NTSTATUS ntstatus;
            IntPtr pOldProtection = Marshal.AllocHGlobal(4);

            ntstatus = NativeMethods.NtProtectVirtualMemory(
                hProcess,
                ref pBaseAddress,
                ref nSizeToUpdate,
                newProtection,
                pOldProtection);

            Marshal.FreeHGlobal(pOldProtection);

            return (ntstatus == Win32Consts.STATUS_SUCCESS);
        }


        public static bool WriteMemory(
            IntPtr hProcess,
            IntPtr pWriteAddress,
            IntPtr pDataToWrite,
            uint nSizeToWrite)
        {
            NTSTATUS ntstatus;

            ntstatus = NativeMethods.NtWriteVirtualMemory(
                hProcess,
                pWriteAddress,
                pDataToWrite,
                nSizeToWrite,
                IntPtr.Zero);

            return (ntstatus == Win32Consts.STATUS_SUCCESS);
        }


        public static void ZeroMemory(IntPtr buffer, int size)
        {
            var nullBytes = new byte[size];
            Marshal.Copy(nullBytes, 0, buffer, size);
        }


        public static void ZeroMemory(byte[] buffer, int size)
        {
            var nullBytes = new byte[size];
            Buffer.BlockCopy(nullBytes, 0, buffer, 0, size);
        }
    }
}
