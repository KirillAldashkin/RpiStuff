using RPiRgbLEDMatrix;
using SixLabors.ImageSharp;
using System.Runtime.InteropServices;
using System.Device.Gpio;

using var gpio = new GpioController(PinNumberingScheme.Logical);
gpio.OpenPin(19, PinMode.InputPullDown);
gpio.OpenPin(26, PinMode.InputPullDown);

using var matrix = new RGBLedMatrix(new()
{
    ChainLength = 2,
    DisableHardwarePulsing = true,
    GpioSlowdown = 3
});
var canvas = matrix.CreateOffscreenCanvas();

Console.Write("GIF folder: ");
var folder = Console.ReadLine()!;
var images = Directory.EnumerateFiles(folder).Select(p => Image.Load<Rgb24>(p)).ToArray();

var running = true;
Console.CancelKeyPress += (_, e) =>
{
    running = false;
    e.Cancel = true;
};

var curImage = 0;
var curFrame = 0;
var changedUp = false;
var changedDown = false;
var bytes = new Rgb24[canvas.Width * canvas.Height];

Task.Run(() =>
{
    var prev = gpio.Read(19) == PinValue.High;
    while (running)
    {
        var cur = gpio.Read(19) == PinValue.High;
        if (!prev && cur) changedUp = true;
        prev = cur;
    }
});
Task.Run(() =>
{
    var prev = gpio.Read(26) == PinValue.High;
    while (running)
    {
        var cur = gpio.Read(26) == PinValue.High;
        if (!prev && cur) changedDown = true;
        prev = cur;
    }
});
while (running)
{
    if (changedUp)
    {
        changedUp = false;
        curImage = (curImage + 1) % images.Length;
        curFrame = 0;
    }
    else if (changedDown)
    {
        changedDown = false;
        curImage--;
        if (curImage < 0) curImage = images.Length - 1;
        curFrame = 0;
    }
    else
    {
        curFrame = (curFrame + 1) % images[curImage].Frames.Count;
    }
    for (var i = 0; (i < images[curImage].Frames.Count) && running; ++i)
    {
        matrix.SwapOnVsync(canvas);
        images[curImage].Frames[i].ProcessPixelRows(data =>
        {
            for (var y = 0; y < data.Height; y++)
                data.GetRowSpan(y).CopyTo(bytes.AsSpan(y * data.Width, data.Width));
        });
        canvas.SetPixels(0, 0, canvas.Width, canvas.Height, MemoryMarshal.Cast<Rgb24, RPiRgbLEDMatrix.Color>(bytes));
        Thread.Sleep(images[curImage].Frames[i].Metadata.GetGifMetadata().FrameDelay * 10);
    }
}
gpio.ClosePin(19);
gpio.ClosePin(26);

