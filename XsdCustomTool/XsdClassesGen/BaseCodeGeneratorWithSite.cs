// BaseCodeGeneratorWithSite.cs: The necessary goo to build a VS.NET custom tool
// The implementation of IVsSingleFileGenerator came from Ian Griffiths [igriffiths@develop.com]
// The implementation of IObjectWithSite came from Tony Juricic [toncitech@yahoo.com] @
// http://www.codeguru.com/net_general/DOMTree.html
// The bits that make it (almost) a drop-in replacement for Microsoft.VSDesigner.CodeGenerator
// came from Chris Sells [csells@sellsbrothers.com]
// NOTE: What makes it (almost) a drop-in replacement is the requirement that code generators
// implement the DefaultExtension property, which used to be implemented by BaseCodeGenerator
// with site. I could do that, but it requires a bunch of nasty code for little benefit.
// The only real reason to do this is if you wanted to access the CodeDomProvider provided
// by the site. Details at the BaseCodeGeneratorWithSite sample (which I understand was
// posted because of the MSDN article that Jon and I wrote describing custom tools as one of
// the top 10 cool things about vs.net):
// http://www.gotdotnet.com/userarea/keywordsrch.aspx?keyword=BaseCodeGeneratorWithSite

using System;
using System.Runtime.InteropServices;

// Built to work with vs.net\Common7\IDE\Microsoft.VSDesigner.dll
// and to be a drop-in replacement for the Microsoft.VSDesigner.CodeGenerator
// class that was public in VS.NET 2002, but private in VS.NET 2003
namespace SellsBrothers.VSDesigner.CodeGenerator {
  [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
  [Guid("3634494C-492F-4F91-8009-4541234E4E99")]
  public interface IVsSingleFileGenerator {
    [return:MarshalAs(UnmanagedType.BStr)]
    string GetDefaultExtension();

    void Generate([In, MarshalAs(UnmanagedType.LPWStr)] string  wszInputFilePath,
      [In, MarshalAs(UnmanagedType.BStr)] string  bstrInputFileContents,
      [In, MarshalAs(UnmanagedType.LPWStr)] string  wszDefaultNamespace,
      out IntPtr pbstrOutputFileContents,
      [MarshalAs(UnmanagedType.U4)] out int pbstrOutputFileContentsSize,
      [In, MarshalAs(UnmanagedType.Interface)] IVsGeneratorProgress  pGenerateProgress);
  }

  [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
  [Guid("BED89B98-6EC9-43CB-B0A8-41D6E2D6669D")]
  public interface IVsGeneratorProgress {
    [return:MarshalAs(UnmanagedType.U4)]
    void GeneratorError(
      [In, MarshalAs(UnmanagedType.Bool)] bool fWarning,
      [In, MarshalAs(UnmanagedType.U4)] int dwLevel,
      [In, MarshalAs(UnmanagedType.BStr)] string bstrError,
      [In, MarshalAs(UnmanagedType.U4)] int dwLine,
      [In, MarshalAs(UnmanagedType.U4)] int dwColumn);

    [return:MarshalAs(UnmanagedType.U4)]
    void Progress(
      [In, MarshalAs(UnmanagedType.U4)] int nComplete,
      [In, MarshalAs(UnmanagedType.U4)] int nTotal);
  }

  [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)] 
  [Guid("FC4801A3-2BA9-11CF-A229-00AA003D7352")]
  public interface IObjectWithSite {
    void SetSite(
      [In, MarshalAs(UnmanagedType.IUnknown)] object pUnkSite);
    void GetSite(
      [In] ref Guid riid, [Out] IntPtr ppvSite);
  }

  public abstract class BaseCodeGeneratorWithSite : IVsSingleFileGenerator, IObjectWithSite {
    public abstract byte[] GenerateCode(string fileName, string fileContents);
    public abstract string DefaultExtension { get; }
    private string defaultNamespace = "";
    public string DefaultNamespace {
      get { return defaultNamespace; }
    }

  #region IVsSingleFileGenerator
    public string GetDefaultExtension() {
      return DefaultExtension;
    }

    public void Generate(string wszInputFilePath, string bstrInputFileContents, string wszDefaultNamespace, out IntPtr pbstrOutputFileContents, out int pbstrOutputFileContentsSize, IVsGeneratorProgress pGenerateProgress) {
      pbstrOutputFileContents = new IntPtr();
      pbstrOutputFileContentsSize = 0;

      if( bstrInputFileContents == null ) throw new ArgumentNullException();

      // Cache for simple GenerateCode implementation
      defaultNamespace = wszDefaultNamespace;

      // Call simple GenerateCode implementation
      byte[] codeBytes = GenerateCode(wszInputFilePath, bstrInputFileContents);
      int len = codeBytes.Length;
      pbstrOutputFileContents = Marshal.AllocCoTaskMem(len);
      pbstrOutputFileContentsSize = len;

      Marshal.Copy(codeBytes, 0, pbstrOutputFileContents, len);
    }
  #endregion

  #region IObjectWithSite
    public void SetSite(object pUnkSite) {
      this.site = pUnkSite;
    }

    public void GetSite(ref Guid riid, IntPtr ppvSite) {
      const int E_FAIL = unchecked((int)0x80004005);
      const int E_POINTER = unchecked((int)0x80000005);
      const int E_NOINTERFACE = unchecked((int)0x80000004);

      // No where to put the resulting interface
      if( ppvSite == IntPtr.Zero ) Marshal.ThrowExceptionForHR(E_POINTER);
      Marshal.WriteIntPtr(ppvSite, IntPtr.Zero);

      // No site
      if( this.Site == null ) Marshal.ThrowExceptionForHR(E_FAIL);

      // QI
      IntPtr pUnk = Marshal.GetIUnknownForObject(this.site);
      IntPtr pvSite = IntPtr.Zero;
      int hr = Marshal.QueryInterface(pUnk, ref riid, out pvSite);
      Marshal.Release(pUnk);

      // No matching interface on site
      if( (hr < 0) || (pvSite == IntPtr.Zero) ) Marshal.ThrowExceptionForHR(E_NOINTERFACE);

      // Return requested interface
      Marshal.WriteIntPtr(ppvSite, pvSite);
    }
  #endregion

    private object site = null;
    protected object Site {
      get { return site; }
      set { site = value; }
    }

  }

}











