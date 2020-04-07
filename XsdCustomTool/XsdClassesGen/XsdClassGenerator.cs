// Wrapper around 'xsd.exe foo.xsd /classes'
#region Copyright © 2002 Chris Sells
/* This software is provided 'as-is', without any express or implied warranty.
 * In no event will the authors be held liable for any damages arising from the
 * use of this software.
 * 
 * Permission is granted to anyone to use this software for any purpose,
 * including commercial applications, subject to the following restrictions:
 * 
 * 1. The origin of this software must not be misrepresented; you must not
 *    claim that you wrote the original software. If you use this software in a
 *    product, an acknowledgment in the product documentation is requested, as
 *    shown here:
 * 
 *    Portions copyright © 2002 Chris Sells (http://www.sellsbrothers.com/).
 * 
 * 2. No substantial portion of this source code may be redistributed without
 *    the express written permission of the copyright holders, where
 *    "substantial" is defined as enough code to be recognizably from this code.
 */
#endregion
// History:
// 11/4/05:
//  -Updated to work w/ VS 2005 beta 1 [stephane.tombeur@gmail.com]
// 4/29/03:
//  -Updated to work w/ VS.NET 2002 or 2003.
// 7/2/02:
//  -Martin Naughton [martin_naughton@rocketmail.com] pointed out a problem w/
//   spaces in the path. Thanks, Martin.
// 6/4/02:
//  -Eric Friedman [ebf@users.sourceforge.net] added namespace support. Thanks, Eric!
//  -Eric also added uninstall, but who'd want to use that?!? : )
// 5/17/02: Initial release

using System;
using System.Diagnostics;   // Process
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using Microsoft.Win32;  // Registry
using SellsBrothers.VSDesigner.CodeGenerator; // Replacement for VS.NET design classes

/* Registration:
 * c:/> regasm /codebase XsdClassGenerator.dll
 * You may also need to run devenv /setup if you find that you get the error "XsdClassGenerator custom tool not found"
 * 
 * Unregistration:
 * c:/> regasm /unregister XsdClassGenerator.dll
 *
 * Usage:
 * Add a .xsd file in the correct format and set:
 *  Build Action: None
 *  Custom Tool: XsdClassGenerator
 */

namespace XsdClassGenerator
{
   public class XsdClassGenerator
   {
      public const string CSharpGeneratorGuid = "3D05404F-6ECF-42b2-8435-2AB63382FFA2";
      public const string VBGeneratorGuid = "F8F34971-A8B5-48bc-8FBA-924E44A942CC";
      public XsdClassGenerator(string language)
      {
         this.language = language;
      }

      protected static string GetStringFromFile(string fileName)
      {
         using (StreamReader reader = new StreamReader(fileName))
         {
            return reader.ReadToEnd();
         }
      }

      public string GenerateCodeFromXsd(string xsdFile, string defaultNamespace)
      {
         string outDir = Path.GetTempPath();
         if (outDir[outDir.Length - 1] == Path.DirectorySeparatorChar ||
             outDir[outDir.Length - 1] == Path.AltDirectorySeparatorChar)
            outDir = outDir.Remove(outDir.Length - 1);
         string csFile = Path.GetFileName(Path.ChangeExtension(xsdFile, language.ToLower()));
         string fullOutputPath = Path.Combine(outDir, csFile);

         try
         {
            // Find xsd.exe
            // TODO: Would be nice to use same BCL classes that xsd.exe uses,
            //		if they're available.
            string xsdExe = FindXSDPath();

            // Fire up xsd.exe
            ProcessStartInfo info = new ProcessStartInfo();
            info.FileName = xsdExe;
            info.Arguments = "\"" + xsdFile + "\"" +
              " /classes" +
              " /l:" + language.ToLower() +
              (!String.IsNullOrEmpty(defaultNamespace) ? " /n:" + defaultNamespace : "") +
              " /o:\"" + outDir + "\"";
            info.CreateNoWindow = true;
            info.RedirectStandardError = true;
            info.RedirectStandardInput = true;
            info.RedirectStandardOutput = true;
            info.UseShellExecute = false;
            using (Process process = Process.Start(info))
               process.WaitForExit();

            // Harvest output
            return GetStringFromFile(fullOutputPath);
         }
         finally
         {
            // Delete temp files
            // (NOTE: System.IO.File guarantees that exceptions are not thrown if files don't exist)
            File.Delete(fullOutputPath);
         }
      }

      private static string FindXSDPath()
      {
         // New method, the paths are now windows SDK directories.
         // Order is : 7.1, 7.0A, 6.0A
         using (RegistryKey rootkey = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Microsoft SDKs"))
         {
            Version runtimeVersion = System.Environment.Version;
            string fullPath = String.Empty;
            RegistryKey subKey;

            string[] SDKLocations = new string[]
            {
               @"NETFXSDK\4.6.1",
               @"NETFXSDK\4.6",
               @"Windows\v10.0",
               @"Windows\v10.0A",
               @"Windows\v8.1",
               @"Windows\v8.1A",
               @"Windows\v8.0",
               @"Windows\v8.0A",
               @"Windows\v7.1",
               @"Windows\v7.1A",
               @"Windows\v7.0",
               @"Windows\v7.0A",
            };

            foreach (string key in SDKLocations)
            {
               using (subKey = rootkey.OpenSubKey(key))
               {
                  if (subKey == null)
                  {
                     continue;
                  }

                  string toolKeyName = runtimeVersion >= new Version("4.0") ? "WinSDK-NetFx40Tools" : "WinSDKNetFx35Tools";
                  using (RegistryKey toolKey = subKey.OpenSubKey(toolKeyName))
                  {
                     if (toolKey == null)
                     {
                        continue;
                     }

                     fullPath = (string)toolKey.GetValue("InstallationFolder");
                     fullPath = Path.Combine(fullPath, "xsd.exe");
                     if (!File.Exists(fullPath))
                     {
                        // Just try find earlier version
                        continue;
                     }

                     return fullPath;
                  }
               }
            }

            throw new NotSupportedException("Cannot find any SDK for runtime version " + runtimeVersion);
         }
      }

      protected string language;
   }

   public abstract class VsXsdClassGenerator : BaseCodeGeneratorWithSite
   {
      public byte[] GenerateCode(XsdClassGenerator generator, string fileName, string fileContents)
      {
         string code = "";
         try
         {
            code = generator.GenerateCodeFromXsd(fileName, this.DefaultNamespace);
         }
         catch (Exception e)
         {
            code = "***ERROR***\n" + e.Message;
         }
         byte[] preamble = System.Text.Encoding.UTF8.GetPreamble();
         return preamble.Concat(System.Text.Encoding.UTF8.GetBytes(code)).ToArray();
      }

      protected static Guid CSharpCategoryGuid = new Guid("{FAE04EC1-301F-11D3-BF4B-00C04F79EFBC}");
      protected static Guid VBCategoryGuid = new Guid("{164B10B9-B200-11D0-8C61-00A0C91E29D5}");

      protected static string GetKeyName(Guid categoryGuid, string edition, Version vsVersion)
      {
         string ver = vsVersion.ToString();
         return @"SOFTWARE\Microsoft\" + edition + "\\" + ver + @"\Generators\{" + categoryGuid.ToString() + @"}\XsdClassGenerator";
      }

      protected static void RegisterCustomTool(Guid categoryGuid, Guid generatorGuid, string desc, Version vsVersion)
      {
         foreach (string edition in new string[] { "VisualStudio", "VCSExpress", "VBExpress", "WDExpress" })
         {
            using (RegistryKey key = Registry.LocalMachine.CreateSubKey(GetKeyName(categoryGuid, edition, vsVersion)))
            {
               key.SetValue("", desc);
               key.SetValue("CLSID", "{" + generatorGuid + "}");
               key.SetValue("GeneratesDesignTimeSource", 1);
            }
         }
      }

      protected static void UnregisterCustomTool(Guid categoryGuid, Version vsVersion)
      {
         foreach (string edition in new string[] { "VisualStudio", "VCSExpress", "VBExpress", "WDExpress" })
         {
            Registry.LocalMachine.DeleteSubKey(GetKeyName(categoryGuid, edition, vsVersion), false);
         }
      }
   }

   [Guid(XsdClassGenerator.CSharpGeneratorGuid)]
   public class VsCSharpXsdClassGenerator : VsXsdClassGenerator
   {
      public override string DefaultExtension { get { return ".cs"; } }

      public override byte[] GenerateCode(string fileName, string fileContents)
      {
         //System.Windows.Forms.MessageBox.Show("Debug Me");
         //System.Diagnostics.Debugger.Break();
         return GenerateCode(new XsdClassGenerator("CS"), fileName, fileContents);
      }

      [ComRegisterFunction]
      public static void RegisterClass(Type t)
      {
         Guid category = CSharpCategoryGuid;
         Guid generator = new Guid(XsdClassGenerator.CSharpGeneratorGuid);
         string desc = "Sells Brothers C# Code Generator for XSD classes";

         // Only Support VS 2008, 2010
         RegisterCustomTool(category, generator, desc, new Version(10, 0));
         RegisterCustomTool(category, generator, desc, new Version(11, 0));
         RegisterCustomTool(category, generator, desc, new Version(12, 0));
         RegisterCustomTool(category, generator, desc, new Version(14, 0));
         RegisterCustomTool(category, generator, desc, new Version(15, 0));
      }

      [ComUnregisterFunction]
      public static void UnregisterClass(Type t)
      {
         Guid category = CSharpCategoryGuid;

         // CHANGED for VS2005
         // Should work for both VS.NET 2002, 2003 and 2005B1
         UnregisterCustomTool(category, new Version(7, 0));
         UnregisterCustomTool(category, new Version(7, 1));
         UnregisterCustomTool(category, new Version(8, 0));
         UnregisterCustomTool(category, new Version(9, 0));
         UnregisterCustomTool(category, new Version(9, 1));
         UnregisterCustomTool(category, new Version(10, 0));
         UnregisterCustomTool(category, new Version(11, 0));
         UnregisterCustomTool(category, new Version(12, 0));
         UnregisterCustomTool(category, new Version(14, 0));
         UnregisterCustomTool(category, new Version(15, 0));
      }
   }

   [Guid(XsdClassGenerator.VBGeneratorGuid)]
   public class VsVBXsdClassGenerator : VsXsdClassGenerator
   {
      public override string DefaultExtension { get { return ".vb"; } }

      public override byte[] GenerateCode(string fileName, string fileContents)
      {
         return GenerateCode(new XsdClassGenerator("VB"), fileName, fileContents);
      }

      [ComRegisterFunction]
      public static void RegisterClass(Type t)
      {
         Guid category = VBCategoryGuid;
         Guid generator = new Guid(XsdClassGenerator.VBGeneratorGuid);
         string desc = "Sells Brothers VB Code Generator for XSD classes";

         // Only Support VS 2008, 2010
         RegisterCustomTool(category, generator, desc, new Version(10, 0));
         RegisterCustomTool(category, generator, desc, new Version(11, 0));
         RegisterCustomTool(category, generator, desc, new Version(12, 0));
         RegisterCustomTool(category, generator, desc, new Version(14, 0));
         RegisterCustomTool(category, generator, desc, new Version(15, 0));
      }

      [ComUnregisterFunction]
      public static void UnregisterClass(Type t)
      {
         Guid category = VBCategoryGuid;

         // CHANGED for VS2005
         // Should work for both VS.NET 2002, 2003 and 2005
         UnregisterCustomTool(category, new Version(7, 0));
         UnregisterCustomTool(category, new Version(7, 1));
         UnregisterCustomTool(category, new Version(8, 0));
         UnregisterCustomTool(category, new Version(9, 0));
         UnregisterCustomTool(category, new Version(9, 1));
         UnregisterCustomTool(category, new Version(10, 0));
         UnregisterCustomTool(category, new Version(11, 0));
         UnregisterCustomTool(category, new Version(12, 0));
         UnregisterCustomTool(category, new Version(14, 0));
         UnregisterCustomTool(category, new Version(15, 0));
      }
   }
}
