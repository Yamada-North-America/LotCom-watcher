using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using WixSharp;

namespace LotComWatcherSetup
{
    // Adapted from LotCom-watcher-setup at 545d153fb74be934d9c81abb0030bc16591604dd.
    internal static class Program
    {
        private static int Main(string[] args)
        {
            try
            {
                if (args.Length != 3)
                    throw new ArgumentException("Usage: setup <version> <publish-directory> <output-directory>");
                var version = new Version(args[0]);
                if (version.Build < 0 || version.Revision != -1 || version.Major > 255 ||
                    version.Minor > 255 || version.Build > 65535)
                    throw new ArgumentException("Use a three-part MSI version (major/minor <= 255, patch <= 65535).");
                string publish = Path.GetFullPath(args[1]);
                string output = Path.GetFullPath(args[2]);
                string executable = Path.Combine(publish, "LotComWatcher.exe");
                if (!System.IO.File.Exists(executable))
                    throw new FileNotFoundException("Publish LotComWatcher before packaging it.", executable);
                Directory.CreateDirectory(output);

                var project = new Project("LotCom Watcher",
                    new Dir(@"%CommonAppDataFolder%\Yamada North America\LotCom Watcher",
                        new Files(Path.Combine(publish, "*"))),
                    new CloseApplication(new Id("LotComWatcher_" + version), "LotComWatcher.exe", false, false)
                    {
                        Timeout = 15
                    });
                // Keep the existing product-code algorithm and upgrade identity.
                project.ProductId = GenerateProductId("LotCom Watcher " + version);
                project.UpgradeCode = new Guid("B8922687-A74A-45D4-96AA-17C91224DB7A");
                project.Version = version;
                project.MajorUpgradeStrategy = MajorUpgradeStrategy.Default;
                project.Platform = Platform.x64;
                project.Scope = InstallScope.perMachine;
                project.ControlPanelInfo.Comments = "LotCom Watcher Application";
                project.ControlPanelInfo.HelpTelephone = "(937) 260-9790";
                project.ControlPanelInfo.UrlInfoAbout = "https://github.com/Yamada-North-America/LotCom-watcher/blob/stable/README.md";
                project.ControlPanelInfo.Contact = "YNA IT";
                project.ControlPanelInfo.Manufacturer = "Yamada North America";
                project.ControlPanelInfo.InstallLocation = "[INSTALLDIR]";
                project.ControlPanelInfo.NoModify = true;
                project.ControlPanelInfo.NoRepair = true;
                project.SourceBaseDir = publish;
                project.OutDir = output;
                project.OutFileName = "LotComWatcher_" + version;
                // No custom License.rtf is tracked by the original setup repository.
                project.UI = WUI.WixUI_ProgressOnly;
                WixExtension.UI.PreferredVersion = "4.0.6";
                WixExtension.Util.PreferredVersion = "4.0.6";
                project.ResolveWildCards();
                var exe = project.AllFiles.Single(f => string.Equals(Path.GetFullPath(f.Name), executable,
                    StringComparison.OrdinalIgnoreCase));
                exe.Shortcuts = new[]
                {
                    new FileShortcut("LotCom Watcher", @"%StartMenuFolder%"),
                    new FileShortcut("LotCom Watcher", @"%Desktop%")
                };
                string msi = project.BuildMsi();
                if (string.IsNullOrEmpty(msi) || !System.IO.File.Exists(msi))
                    throw new InvalidOperationException("WiX did not produce an MSI.");
                return 0;
            }
            catch (Exception error)
            {
                Console.Error.WriteLine(error);
                return 1;
            }
        }

        private static Guid GenerateProductId(string input)
        {
            using (var sha = SHA256.Create())
            {
                byte[] guid = new byte[16];
                Array.Copy(sha.ComputeHash(Encoding.UTF8.GetBytes(input)), guid, guid.Length);
                return new Guid(guid);
            }
        }
    }
}
