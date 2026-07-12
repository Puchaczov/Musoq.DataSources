using System;
using System.Runtime.InteropServices;

namespace Musoq.DataSources.Os.Runtime;

internal sealed class RuntimeEntity
{
    public string DotNetVersion => Environment.Version.ToString();
    public string FrameworkDescription => RuntimeInformation.FrameworkDescription;
    public string OSDescription => RuntimeInformation.OSDescription;
    public string OSArchitecture => RuntimeInformation.OSArchitecture.ToString();
    public string ProcessArchitecture => RuntimeInformation.ProcessArchitecture.ToString();
    public bool Is64BitOperatingSystem => Environment.Is64BitOperatingSystem;
    public bool Is64BitProcess => Environment.Is64BitProcess;
    public int ProcessorCount => Environment.ProcessorCount;
    public string CurrentDirectory => Environment.CurrentDirectory;
}
