﻿using CCodeAI;
using System.Reflection;
using System.Resources;
using System.Runtime.InteropServices;

[assembly: AssemblyTitle(Vsix.Name)]
[assembly: AssemblyDescription(Vsix.Description)]
[assembly: AssemblyConfiguration("")]
[assembly: AssemblyCompany(Vsix.Author)]
[assembly: AssemblyProduct(Vsix.Name)]
[assembly: AssemblyCopyright(Vsix.Author)]
[assembly: AssemblyTrademark("")]
[assembly: AssemblyCulture("")]

[assembly: ComVisible(false)]

[assembly: AssemblyVersion(Vsix.Version)]
[assembly: AssemblyFileVersion(Vsix.Version)]
[assembly: NeutralResourcesLanguage("en-US", UltimateResourceFallbackLocation.MainAssembly)]
namespace System.Runtime.CompilerServices
{
    public class IsExternalInit { }
}