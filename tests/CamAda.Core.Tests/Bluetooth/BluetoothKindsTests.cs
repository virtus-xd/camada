using CamAda.Core.Bluetooth;

namespace CamAda.Core.Tests.Bluetooth;

public class BluetoothKindsTests
{
    [Theory]
    [InlineData(0x240404u, BluetoothDeviceKind.Audio)]
    [InlineData(0x200418u, BluetoothDeviceKind.Audio)]
    [InlineData(0x002580u, BluetoothDeviceKind.Mouse)]
    [InlineData(0x002540u, BluetoothDeviceKind.Keyboard)]
    [InlineData(0x0025C0u, BluetoothDeviceKind.Keyboard)]
    [InlineData(0x002508u, BluetoothDeviceKind.Gamepad)]
    [InlineData(0x002504u, BluetoothDeviceKind.Gamepad)]
    [InlineData(0x5A020Cu, BluetoothDeviceKind.Phone)]
    [InlineData(0x3E010Cu, BluetoothDeviceKind.Computer)]
    [InlineData(0x000000u, BluetoothDeviceKind.Other)]
    public void Class_of_device(uint cod, BluetoothDeviceKind expected) =>
        Assert.Equal(expected, BluetoothKinds.FromClassOfDevice(cod));

    // Windows'un eşleşmiş cihaz özellikleri (System.Devices.Aep.Bluetooth.Cod.Major/Minor), 24.11 ölçümü:
    // Buds2 4/1, Wireless Controller 5/2.
    [Theory]
    [InlineData(4u, 1u, BluetoothDeviceKind.Audio)]
    [InlineData(5u, 2u, BluetoothDeviceKind.Gamepad)]
    [InlineData(5u, 0x20u, BluetoothDeviceKind.Mouse)]
    [InlineData(5u, 0x10u, BluetoothDeviceKind.Keyboard)]
    [InlineData(2u, 3u, BluetoothDeviceKind.Phone)]
    [InlineData(0u, 0u, BluetoothDeviceKind.Other)]
    public void Major_and_minor_class(uint major, uint minor, BluetoothDeviceKind expected) =>
        Assert.Equal(expected, BluetoothKinds.FromMajorMinor(major, minor));

    [Theory]
    [InlineData((ushort)0x03C1, BluetoothDeviceKind.Keyboard)]
    [InlineData((ushort)0x03C2, BluetoothDeviceKind.Mouse)]
    [InlineData((ushort)0x03C3, BluetoothDeviceKind.Gamepad)]
    [InlineData((ushort)0x03C4, BluetoothDeviceKind.Gamepad)]
    [InlineData((ushort)0x0941, BluetoothDeviceKind.Audio)]
    [InlineData((ushort)0x0841, BluetoothDeviceKind.Audio)]
    [InlineData((ushort)0x0040, BluetoothDeviceKind.Phone)]
    [InlineData((ushort)0x0080, BluetoothDeviceKind.Computer)]
    [InlineData((ushort)0x0000, BluetoothDeviceKind.Other)]
    public void Le_appearance(ushort appearance, BluetoothDeviceKind expected) =>
        Assert.Equal(expected, BluetoothKinds.FromLeAppearance(appearance));

    [Fact]
    public void Only_audio_can_connect()
    {
        Assert.True(BluetoothKinds.CanConnect(BluetoothDeviceKind.Audio));
        Assert.False(BluetoothKinds.CanConnect(BluetoothDeviceKind.Mouse));
        Assert.False(BluetoothKinds.CanConnect(BluetoothDeviceKind.Gamepad));
    }

    [Fact]
    public void Tile_subtitle()
    {
        Assert.Equal("Yok", BluetoothKinds.TileSubtitle(false, false, []));
        Assert.Equal("Kapalı", BluetoothKinds.TileSubtitle(true, false, []));
        Assert.Equal("Açık", BluetoothKinds.TileSubtitle(true, true, []));
        Assert.Equal("Buds2 bağlı", BluetoothKinds.TileSubtitle(true, true, ["Buds2"]));
        Assert.Equal("2 cihaz bağlı", BluetoothKinds.TileSubtitle(true, true, ["Buds2", "R1mouse1"]));
    }
}
