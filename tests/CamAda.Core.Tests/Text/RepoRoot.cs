namespace CamAda.Core.Tests.Text;

/// <summary>Depo kökünü test derlemesinin klasöründen yukarı doğru CamAda.slnx arayarak bulur.</summary>
internal static class RepoRoot
{
    public static string Find()
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
        {
            if (File.Exists(Path.Combine(dir.FullName, "CamAda.slnx")))
            {
                return dir.FullName;
            }
        }

        throw new InvalidOperationException("CamAda.slnx bulunamadı: testler depo içinden çalıştırılmalı.");
    }
}
