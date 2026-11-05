using System.IO;
using Microsoft.Win32;
using OverTranslate.Services;
using Xunit;

namespace OverTranslate.Tests;

// Run against a throwaway key under HKCU, never the real AppUserModelId one.
public class NotificationIdentityTests : IDisposable
{
    private const string Parent = @"Software\OverTranslate.Tests";
    private readonly string _path = $@"{Parent}\{Guid.NewGuid():N}";
    private readonly RegistryKey _root;

    public NotificationIdentityTests() => _root = Registry.CurrentUser.CreateSubKey(_path);

    public void Dispose()
    {
        _root.Dispose();
        Registry.CurrentUser.DeleteSubKeyTree(_path, throwOnMissingSubKey: false);

        bool empty;
        using (var parent = Registry.CurrentUser.OpenSubKey(Parent))
            empty = parent is not null && parent.SubKeyCount == 0 && parent.ValueCount == 0;
        if (empty) Registry.CurrentUser.DeleteSubKey(Parent, throwOnMissingSubKey: false);
    }

    [Fact]
    public void TheIdIsNotTheInstalledApplications()
    {
        Assert.NotEqual("velopack.OverTranslate", NotificationIdentityService.Aumid);
    }

    [Fact]
    public void Write_RecordsTheNameAndIcon()
    {
        Assert.True(NotificationIdentityService.Write(_root, NotificationIdentityService.Aumid, @"C:\Apps\TH\icons\app.ico"));

        using var key = _root.OpenSubKey(NotificationIdentityService.Aumid)!;
        Assert.Equal("OverTranslateTH", key.GetValue("DisplayName"));
        Assert.Equal(@"C:\Apps\TH\icons\app.ico", key.GetValue("IconUri"));
    }

    [Fact]
    public void Write_LeavesAMatchingRegistrationAlone()
    {
        NotificationIdentityService.Write(_root, NotificationIdentityService.Aumid, @"C:\Apps\TH\icons\app.ico");

        Assert.False(NotificationIdentityService.Write(_root, NotificationIdentityService.Aumid, @"C:\Apps\TH\icons\app.ico"));
    }

    [Fact]
    public void Write_FollowsACopyThatMoved()
    {
        NotificationIdentityService.Write(_root, NotificationIdentityService.Aumid, @"C:\Apps\TH\icons\app.ico");

        Assert.True(NotificationIdentityService.Write(_root, NotificationIdentityService.Aumid, @"D:\Portable\TH\icons\app.ico"));

        using var key = _root.OpenSubKey(NotificationIdentityService.Aumid)!;
        Assert.Equal(@"D:\Portable\TH\icons\app.ico", key.GetValue("IconUri"));
    }

    [Fact]
    public void TheIconIsCopiedBesideTheExecutable()
    {
        Assert.True(File.Exists(Path.Combine(AppContext.BaseDirectory, NotificationIdentityService.IconRelativePath)));
    }
}
