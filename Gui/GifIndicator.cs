namespace GW2RaidsGui;

/// <summary>
/// A small animated "working" indicator backed by the embedded bongo-cat.gif.
/// A PictureBox animates an animated GIF on its own; setting the image starts
/// the animation and clearing it stops it, so the timer cost is zero at rest.
/// Falls back to showing nothing if the resource can't be loaded.
/// </summary>
public sealed class GifIndicator : PictureBox
{
    private MemoryStream? _stream;
    private Image? _gif;

    public GifIndicator()
    {
        SizeMode = PictureBoxSizeMode.Zoom; // scale the gif down, preserve aspect
        BackColor = Color.Transparent;
        TabStop = false;
        Visible = false;
        TryLoad();
    }

    public bool IsAvailable => _gif != null;

    private void TryLoad()
    {
        try
        {
            using var res = typeof(GifIndicator).Assembly
                .GetManifestResourceStream("GW2RaidsGui.bongo-cat.gif");
            if (res == null) return;
            // The backing stream must stay open for the lifetime of the image.
            _stream = new MemoryStream();
            res.CopyTo(_stream);
            _stream.Position = 0;
            _gif = Image.FromStream(_stream);
        }
        catch
        {
            // No indicator if the gif can't be decoded; not worth failing over.
        }
    }

    public void Start()
    {
        if (_gif == null) return;
        Image = _gif; // PictureBox begins animating the gif
        Visible = true;
    }

    public void Stop()
    {
        Image = null; // stops the animation
        Visible = false;
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _gif?.Dispose();
            _stream?.Dispose();
        }
        base.Dispose(disposing);
    }
}
