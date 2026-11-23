using System.Windows;
using System.Windows.Controls;

namespace CamAda.Glass;

/// <summary>Adanın hapına cam arka planı veren yöntem. <see cref="IDisposable.Dispose"/> hapı eski hâline döndürür.</summary>
public interface IGlassBackdrop : IDisposable
{
    void Attach(Window host, Border pill);
}
