namespace WardogsFireControl;

internal static class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
        System.Globalization.CultureInfo.DefaultThreadCurrentCulture = System.Globalization.CultureInfo.InvariantCulture;
        if (args.Length == 5 && args[0] == "--verify-ocr")
        {
            try
            {
                using var ocr = new CoordinateOcrService();
                var result = ocr.ReadImageAtPoint(
                    args[1],
                    new System.Drawing.Point(int.Parse(args[2]), int.Parse(args[3])));
                File.WriteAllText(args[4],
                    $"success={result.IsSuccess}{Environment.NewLine}coordinate={result.Coordinate}{Environment.NewLine}raw={result.RawText}");
                Environment.ExitCode = result.IsSuccess ? 0 : 2;
            }
            catch (Exception exception)
            {
                File.WriteAllText(args[4], exception.ToString());
                Environment.ExitCode = 1;
            }
            return;
        }
        ApplicationConfiguration.Initialize();
        Application.Run(new MainForm());
    }
}
