namespace CamAda.Core.Text;

/// <summary>İngilizcede tekil/çoğul ayrımı (Türkçede iki kaynak metni aynı olabilir).</summary>
public static class Plural
{
    public static string Pick(int n, string one, string other) => n == 1 ? one : other;
}
