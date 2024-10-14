using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DrawingNameComposer.Services;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Reflection.PortableExecutable;
using System.Runtime.CompilerServices;
using System.Windows.Media;

using System.Windows;

using TSD = Tekla.Structures.Drawing;

[assembly: InternalsVisibleTo("ServiceTests")]
namespace DrawingNameComposer;

public partial class MainViewModel(
    IMetadataService metadataService,
    IPrintingService printingService,
    IPresetsService presetsService) : ObservableObject
{
    internal void Initialize()
    {
        if (presetsService.FileExists())
        {
            var loaded = presetsService.LoadFromFile();
            if (loaded != null && loaded.Any())
            {
                Presets = [.. loaded];
                SelectedPreset = Presets[0];
            }
        }
        else
        {
            presetsService.SaveToFile(Presets);
        }
        AvailableProperties.AddRange(metadataService.Get());
        if (SelectedPreset != null)
        {
            ChosenProperties.AddRange(SelectedPreset.ChosenProperties);
        }
        PrintSettings.AddRange(printingService.Get());
        this.PropertyChanged += OnPropertyChanged;
        var drawingEvents = new TSD.UI.Events();
        drawingEvents.DrawingListSelectionChanged += DrawingEvents_SelectionChange;
        drawingEvents.Register();
    }

    private void DrawingEvents_SelectionChange()
    {
        ResultForDrawing = Helpers.ComposeResult(Template);
    }

    private void OnPropertyChanged(object sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == "Template")
        {
            ResultForDrawing = Helpers.ComposeResult(Template);
        }
    }


    public List<string> PrintSettings { get; } = [];
    public ObservableCollection<Preset> Presets { get; private set; } = [];
    public ExtendedObservableCollection<string> AvailableProperties { get; } = [];
    public ExtendedObservableCollection<string> ChosenProperties { get; } = [];

    [ObservableProperty]
    private string _template = string.Empty;

    [ObservableProperty]
    private string _resultForDrawing = string.Empty;

    [ObservableProperty]
    private string _saveAsInput = string.Empty;

    [ObservableProperty]
    private Preset? _selectedPreset;

    [ObservableProperty]
    private string _selectedPrintSetting = Environment.UserName;

    /// <summary>
    /// Replaces in the presets the currently selected preset and the properties chosen
    /// </summary>
    [RelayCommand]
    private void OnSave()
    {
        if (SelectedPreset is null) return;
        var existingPresetInList = Presets.FirstOrDefault(x => x.Name.Equals(SelectedPreset.Name));
        if (existingPresetInList is not null)
        {
            existingPresetInList.ChosenProperties = ChosenProperties;
            existingPresetInList.AvailableProperties = AvailableProperties;
            existingPresetInList.PrintSetting = SelectedPrintSetting;
        }
        presetsService.SaveToFile(Presets);
    }


    /// <summary>
    /// Saves a new preset with the given name
    /// </summary>
    [RelayCommand]
    private void OnSaveAs()
    {
        if (string.IsNullOrEmpty(SaveAsInput)) return;
        SaveAsInput = SaveAsInput.Trim();
        Presets.Add(new Preset()
        {
            Name = SaveAsInput,
            ChosenProperties = ChosenProperties,
            AvailableProperties = AvailableProperties,
            PrintSetting = SelectedPrintSetting
        });
        presetsService.SaveToFile(Presets);
    }

    [RelayCommand]
    private void OnLoad()
    {
        if (SelectedPreset is null) return;
        var existingPresetInList = Presets.FirstOrDefault(x => x.Name.Equals(SelectedPreset.Name));
        if (existingPresetInList is not null)
        {
            ChosenProperties.Clear();
            ChosenProperties.AddRange(existingPresetInList.ChosenProperties);
            AvailableProperties.Clear();
            AvailableProperties.AddRange(AvailableProperties);
            SelectedPrintSetting = PrintSettings.FirstOrDefault(x => x == existingPresetInList.PrintSetting) ?? "";
        }
    }

    [RelayCommand]
    private void OnPrint()
    {
    }
}


public class Printer
{

    /// <summary>
    /// This is the default template file found in the model folder under the attributes folder
    /// </summary>
    private static string templateFile = $@"{Path.Combine(new Model().GetInfo().ModelPath, "attributes", "PdfPrintOptions.xml")}";

    /// <summary>
    /// The default value of the drawingFolder, when the given one is missing,
    /// it is a PrinterApp folder in C:\Users\{UserName}\AppData\Local\PrinterApp
    /// </summary>
    private static string drawingFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "PrinterApp");

    /// <summary>
    /// The default value of the bookletFolder, when the given one is missing,
    /// it is a PrinterApp folder in C:\Users\{UserName}\AppData\Local\PrinterApp
    /// </summary>
    private static string bookletFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "PrinterApp");

    /// <summary>
    /// The XML type file
    /// </summary>
    public static string TemplateFile
    {
        get => templateFile;
        set { if (value != null || value != string.Empty || Uri.IsWellFormedUriString(value, UriKind.Absolute)) templateFile = value; }
    }

    /// <summary>
    /// Tracks the number of drawings that failed to print
    /// </summary>
    private static int numberOfDrawingsFailed;

    /// <summary>
    /// Tracks the number of drawing skipped by user
    /// </summary>
    private static int numberOfDrawingsSkipped;

    /// <summary>
    /// Used to print the log to a file
    /// </summary>
    private static readonly List<string> logFile = new List<string>();

    /// <summary>
    /// The path to the DPM Printer executable
    /// </summary>
    private static string BinPath
    {
        get
        {
            var dpmPath = @"applications\Tekla\Model\DPMPrinter\DPMPrinterCommand.exe";
            string binPath_ = string.Empty;
            Tekla.Structures.TeklaStructuresSettings.GetAdvancedOption("XSBIN", ref binPath_);
            return Path.Combine(binPath_.Replace(@"\\", @"\"), dpmPath);
        }
    }

    /// <summary>
    /// The drawing folder
    /// </summary>
    public static string DrawingFolder
    {
        get => drawingFolder;
        set { if (value != null || value != string.Empty || !Uri.IsWellFormedUriString(value, UriKind.Absolute)) drawingFolder = value; }
    }

    /// <summary>
    /// The booklet folder
    /// </summary>
    public static string BookletFolder
    {
        get => bookletFolder;
        set { if (value != null || value != string.Empty || !Uri.IsWellFormedUriString(value, UriKind.Absolute)) bookletFolder = value; }
    }

    /// <summary>
    /// The dictionary that ties the drawing to it's most current dg.DPM file
    /// </summary>
    private static readonly Dictionary<string, string> metadataWithGUIDs = GetGUIDsFromMetadataFiles(new Model().GetInfo().ModelPath);

    /// <summary>
    /// Main method
    /// </summary>
    /// <param name="deleteDrawings"></param>
    public static void PrintDrawingCommand(bool deleteDrawings, bool printSnapshot)
    {
        try
        {
            //The double quotation marks are needed to pass the arguments to the DPM Printer Proccess
            string settingsFile = string.Format(@"""{0}""", templateFile);

            Console.ForegroundColor = ConsoleColor.Green;
            Stopwatch stopwatch = Stopwatch.StartNew();
            Log($"====================Printing started: {DateTime.Now}=========================");
            Log($"----------------------------------------------------------------------------------");

            DrawingEnumerator drawingsEnum = new DrawingHandler().GetDrawingSelector().GetSelected();

            if (drawingsEnum.GetSize() == 0)
            {
                MessageBox.Show("No drawings selected!");
                return;
            }

            List<Drawing> alldrawings = new List<Drawing>();
            List<string> drawingNames = new List<string>();
            foreach (Drawing drawing in drawingsEnum)
            {
                alldrawings.Add(drawing);
                drawingNames.Add(drawing.Name);
            }
            //The names of the drawings are needed to figure out how many booklets there, this is available only for Femern projects
            var drawingsCount = alldrawings.Count;
            Log($"{drawingsCount} drawings to be printed.");
            drawingNames = drawingNames.Distinct().ToList();
            int numberofProcesses = (int)Math.Floor((decimal)Environment.ProcessorCount) / 2;

            var numberOfBooklets = drawingNames.Count;
            //A way to figure out how to handle the printing
            //for example 3 booklets containing : 1, 2, 2 drawings each will print slower
            //by booklets in parallel than printing in parallel all 5 drawings separately
            if (printSnapshot)
            {
                if (numberOfBooklets > numberofProcesses)
                {
                    CreateBookletInParallel(alldrawings, drawingNames);
                }
                else
                {
                    PrintDrawingsInParallel(alldrawings);
                }
            }
            else
            {
                if (numberOfBooklets > numberofProcesses)
                {
                    CreateBookletTraditional(alldrawings, drawingNames);
                }
                else
                {
                    PrintDrawingsTraditional(alldrawings);
                }
            }

            //Creates the merged pdf and deletes if asked by user the separate drawing
            FilesHandler(deleteDrawings, alldrawings, drawingNames);

            #region Logging

            Console.ForegroundColor = ConsoleColor.Green;
            Log($"----------------------------------------------------------------------------------");
            Log($"{numberOfDrawingsSkipped} drawings skipped.");
            Log($"{numberOfDrawingsFailed} drawings failed.");
            Log($"{drawingsCount - numberOfDrawingsSkipped} drawings printed in: {stopwatch.Elapsed.Minutes} min and {stopwatch.Elapsed.Seconds} s");
            Log($"=====================Printing done: {DateTime.Now}===========================");
            Log("");
            InformationDialog informationDialog = new InformationDialog();
            informationDialog.label1.Text = $"The drawings and the booklet have been created using:\n" +
                $"The settings: {settingsFile}\n" +
                $"In the location:\n" +
                $"Drawing folder: {drawingFolder}\n" +
                $"Booklet folder: {bookletFolder}";
            informationDialog.Update();
            informationDialog.ShowDialog();
            stopwatch.Stop();
            Console.ForegroundColor = ConsoleColor.White;
            LogFinal();
            #endregion Logging
        }
        catch (System.IO.IOException ex)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Log(ex.Message);
            Console.ForegroundColor = ConsoleColor.White;
            Maintenance.ErrorLogger(ex);
        }
        catch (Exception ex)
        {
            Maintenance.ErrorLogger(ex);
        }
    }

    /// <summary>
    /// Sends messages to the console and appends them to a list for printing on file at the end
    /// </summary>
    /// <param name="message"></param>
    public static void Log(string message)
    {
        logFile.Add(message);
        Console.WriteLine(message);
    }

    /// <summary>
    /// Prints the final log to file
    /// </summary>
    private static void LogFinal()
    {
        Maintenance.EventLogger(logFile);
    }

    /// <summary>
    /// Creates booklets by Femern rules and deletes the separate drawings if asked
    /// </summary>
    /// <param name="deleteDrawings"></param>
    /// <param name="alldrawings"></param>
    /// <param name="drawingNames"></param>
    private static void FilesHandler(bool deleteDrawings, List<Drawing> alldrawings, List<string> drawingNames)
    {
        string combinefileName = string.Empty;
        string bookletFolderName = string.Empty;
        List<string> pdfs = new List<string>();
        drawingNames.ForEach(drawingName =>
        {
            List<Drawing> booklet = Booklet(alldrawings, drawingName);

            booklet.ForEach(drawing =>
            {
                string outputfile = string.Format(@"""{3}\{0}-{1}-{2}.pdf""", drawing.Name, drawing.Title3, drawing.Mark, drawingFolder);
                string plotName = drawing.GetPlotFileName(false).Replace("COW", "CN3");
                combinefileName = string.Format(@"{1}\{0}\{0}.pdf", plotName, bookletFolder);
                bookletFolderName = $@"{bookletFolder}\{plotName}";
                outputfile = outputfile.Substring(1, outputfile.Length - 2);
                if (File.Exists(outputfile))
                    pdfs.Add(outputfile);
            });
            MergePDFs(bookletFolderName, combinefileName, pdfs);
            if (deleteDrawings)
            {
                DeleteFiles(pdfs);
            }
            pdfs.Clear();
        });
    }

    /// <summary>
    /// Creates the booklets in parallel while checking for a valid snapshot
    /// If it doesn't find a valid snapshot it will ask the user to choose
    /// Between printing throught the normal way or skipping the drawing altogether
    /// </summary>
    /// <param name="alldrawings"></param>
    /// <param name="drawingNames"></param>
    private static void CreateBookletInParallel(List<Drawing> alldrawings, List<string> drawingNames)
    {
        ParallelOptions parallelOptions = new ParallelOptions()
        {
            MaxDegreeOfParallelism = (int)Math.Floor((decimal)Environment.ProcessorCount)
        };
        Parallel.ForEach(
            drawingNames.AsParallel().AsOrdered(),
            parallelOptions,
            (drawingName, state, index) =>
            {
                List<Drawing> booklet = Booklet(alldrawings, drawingName);

                int x = 0;
                booklet.ForEach(drawing =>
                {
                    Console.ForegroundColor = ConsoleColor.White;
                    Log($"Printing drawing no.{x + 1} - Mark: {booklet[x].Mark} of booklet no.{index + 1} - Name:{drawingName}");
                    if (PrintSingleDrawing(drawing))
                    {
                        Console.ForegroundColor = ConsoleColor.Green;
                        Log($"Drawing no.{x + 1} - Mark: {booklet[x].Mark} of booklet no.{index + 1} - Name:{drawingName} printed successfully");
                    }
                    else
                    {
                        var result = MessageBox.Show($"The drawing: {drawing.Mark}-{drawing.Name}-{drawing.Title1} could not be printed,\nmake sure that the drawing has a valid snapshot.\nIf you wish to print the drawing using the default method press Yes.", "Not able to print drawing", MessageBoxButtons.YesNo);
                        switch (result)
                        {
                            case DialogResult.Yes:
                                {
                                    Console.ForegroundColor = ConsoleColor.White;
                                    Log($"Drawing no.{x + 1} - Mark: {booklet[x].Mark} of booklet no.{index + 1} - Name:{drawingName} traditional printing started. Please wait...");
                                    switch (PrintSingleDrawingTraditional(drawing))
                                    {
                                        case true:
                                            {
                                                Console.ForegroundColor = ConsoleColor.Green;
                                                Log($"Drawing no.{x + 1} - Mark: {booklet[x].Mark} of booklet no.{index + 1} - Name:{drawingName} traditional printing finished.");
                                            }
                                            break;

                                        case false:
                                            {
                                                Console.ForegroundColor = ConsoleColor.Red;
                                                numberOfDrawingsFailed++;
                                                Log($"Drawing no.{x + 1} - Mark: {booklet[x].Mark} of booklet no.{index + 1} - Name:{drawingName} traditional printing failed.");
                                            }
                                            break;
                                    }
                                }
                                break;

                            case DialogResult.No:
                                {
                                    Log($"Drawing no.{x + 1} - Mark: {booklet[x].Mark} of booklet no.{index + 1} - Name:{drawingName} printing skipped.");
                                    numberOfDrawingsSkipped++;
                                    return;
                                }
                        }
                    }
                    x++;
                });
            });
    }

    /// <summary>
    /// Creates the booklets in parallel while checking for a valid snapshot
    /// If it doesn't find a valid snapshot it will ask the user to choose
    /// Between printing throught the normal way or skipping the drawing altogether
    /// </summary>
    /// <param name="alldrawings"></param>
    /// <param name="drawingNames"></param>
    private static void CreateBookletTraditional(List<Drawing> alldrawings, List<string> drawingNames)
    {
        ParallelOptions parallelOptions = new ParallelOptions()
        {
            MaxDegreeOfParallelism = (int)Math.Floor((decimal)Environment.ProcessorCount)
        };

        drawingNames.ForEach(drawingName =>
        {
            List<Drawing> booklet = Booklet(alldrawings, drawingName);
            int x = 0;
            int y = 0;
            booklet.ForEach(drawing =>
            {
                Console.ForegroundColor = ConsoleColor.White;
                Log($"Printing drawing no.{x + 1} - Mark: {booklet[x].Mark} of booklet no.{y + 1} - Name:{drawingName}");
                if (PrintSingleDrawingTraditional(drawing))
                {
                    Console.ForegroundColor = ConsoleColor.Green;
                    Log($"Drawing no.{x + 1} - Mark: {booklet[x].Mark} of booklet no.{y + 1} - Name:{drawingName} printed successfully");
                }
                else
                {
                    var result = MessageBox.Show($"The drawing: {drawing.Mark}-{drawing.Name}-{drawing.Title1} could not be printed,\nmake sure that the drawing has a valid snapshot.\nIf you wish to print the drawing using the default method press Yes.", "Not able to print drawing", MessageBoxButtons.YesNo);
                    switch (result)
                    {
                        case DialogResult.Yes:
                            {
                                Console.ForegroundColor = ConsoleColor.White;
                                Log($"Drawing no.{x + 1} - Mark: {booklet[x].Mark} of booklet no.{y + 1} - Name:{drawingName} traditional printing started. Please wait...");
                                switch (PrintSingleDrawingTraditional(drawing))
                                {
                                    case true:
                                        {
                                            Console.ForegroundColor = ConsoleColor.Green;
                                            Log($"Drawing no.{x + 1} - Mark: {booklet[x].Mark} of booklet no.{y + 1} - Name:{drawingName} traditional printing finished.");
                                        }
                                        break;

                                    case false:
                                        {
                                            Console.ForegroundColor = ConsoleColor.Red;
                                            numberOfDrawingsFailed++;
                                            Log($"Drawing no.{x + 1} - Mark: {booklet[x].Mark} of booklet no.{y + 1} - Name:{drawingName} traditional printing failed.");
                                        }
                                        break;
                                }
                            }
                            break;

                        case DialogResult.No:
                            {
                                Log($"Drawing no.{x + 1} - Mark: {booklet[x].Mark} of booklet no.{y + 1} - Name:{drawingName} printing skipped.");
                                numberOfDrawingsSkipped++;
                                return;
                            }
                    }
                }
                x++;
                y++;
            });
        });
    }

    /// <summary>
    /// This does the same as the PrintBookletinParallel only that will ignore any booklets
    /// and just print all the drawings in Parallel
    /// </summary>
    /// <param name="booklet"></param>
    private static void PrintDrawingsInParallel(List<Drawing> booklet)
    {
        ParallelOptions parallelOptions = new ParallelOptions()
        {
            MaxDegreeOfParallelism = (int)Math.Floor((decimal)2 * Environment.ProcessorCount)
        };

        Parallel.ForEach(booklet.AsParallel().AsOrdered(), parallelOptions, (drawing, state, index) =>
        {
            if (drawing != null && drawing.Mark.Trim() != string.Empty)
            {
                Console.ForegroundColor = ConsoleColor.White;
                Log($"Printing drawing no {index + 1}: {booklet[(int)index].Mark} out of {booklet.Count}");
                if (PrintSingleDrawing(drawing))
                {
                    Console.ForegroundColor = ConsoleColor.Green;
                    Log($"Drawing no {index + 1}: {booklet[(int)index].Mark} printed successfully");
                }
                else
                {
                    var result = MessageBox.Show($"The drawing: {drawing.Mark}-{drawing.Name}-{drawing.Title1} could not be printed,\nmake sure that the drawing has a valid snapshot.\nIf you wish to print the drawing using the default method press Yes.", "Not able to print drawing", MessageBoxButtons.YesNo);
                    switch (result)
                    {
                        case DialogResult.Yes:
                            {
                                Console.ForegroundColor = ConsoleColor.White;
                                Log($"Drawing no {index + 1}: {booklet[(int)index].Mark} traditional printing started. Please wait...");
                                switch (PrintSingleDrawingTraditional(drawing))
                                {
                                    case true:
                                        {
                                            Console.ForegroundColor = ConsoleColor.Green;
                                            Log($"Drawing no {index + 1}: {booklet[(int)index].Mark} printed traditionally successfully");
                                        }
                                        break;

                                    case false:
                                        {
                                            numberOfDrawingsFailed++;
                                            Console.ForegroundColor = ConsoleColor.Red;
                                            Log($"Drawing no {index + 1}: {booklet[(int)index].Mark} printed traditionally failed. The drawing might not be up to date.");
                                        }
                                        break;
                                }
                            }
                            break;

                        case DialogResult.No:
                            {
                                Console.ForegroundColor = ConsoleColor.White;
                                Log($"Drawing no {index + 1}: {booklet[(int)index].Mark} printing skipped.");
                                numberOfDrawingsSkipped++;
                            }
                            break;
                    }
                }
            }
        });
    }

    /// <summary>
    /// This does the same as the PrintBookletinParallel only that will ignore any booklets
    /// and just print all the drawings in Parallel
    /// </summary>
    /// <param name="booklet"></param>
    private static void PrintDrawingsTraditional(List<Drawing> booklet)
    {
        ParallelOptions parallelOptions = new ParallelOptions()
        {
            MaxDegreeOfParallelism = (int)Math.Floor((decimal)2 * Environment.ProcessorCount)
        };
        int x = 0;
        booklet.ForEach(drawing =>
        {
            if (drawing != null && drawing.Mark.Trim() != string.Empty)
            {
                Console.ForegroundColor = ConsoleColor.White;
                Log($"Printing drawing no {x + 1}: {booklet[(int)x].Mark} out of {booklet.Count}");
                if (PrintSingleDrawingTraditional(drawing))
                {
                    Console.ForegroundColor = ConsoleColor.Green;
                    Log($"Drawing no {x + 1}: {booklet[(int)x].Mark} printed successfully");
                }
                else
                {
                    var result = MessageBox.Show($"The drawing: {drawing.Mark}-{drawing.Name}-{drawing.Title1} could not be printed,\nmake sure that the drawing has a valid snapshot.\nIf you wish to print the drawing using the default method press Yes.", "Not able to print drawing", MessageBoxButtons.YesNo);
                    switch (result)
                    {
                        case DialogResult.Yes:
                            {
                                Console.ForegroundColor = ConsoleColor.White;
                                Log($"Drawing no {x + 1}: {booklet[(int)x].Mark} traditional printing started. Please wait...");
                                switch (PrintSingleDrawingTraditional(drawing))
                                {
                                    case true:
                                        {
                                            Console.ForegroundColor = ConsoleColor.Green;
                                            Log($"Drawing no {x + 1}: {booklet[(int)x].Mark} printed traditionally successfully");
                                        }
                                        break;

                                    case false:
                                        {
                                            numberOfDrawingsFailed++;
                                            Console.ForegroundColor = ConsoleColor.Red;
                                            Log($"Drawing no {x + 1}: {booklet[(int)x].Mark} printed traditionally failed. The drawing might not be up to date.");
                                        }
                                        break;
                                }
                            }
                            break;

                        case DialogResult.No:
                            {
                                Console.ForegroundColor = ConsoleColor.White;
                                Log($"Drawing no {x + 1}: {booklet[(int)x].Mark} printing skipped.");
                                numberOfDrawingsSkipped++;
                            }
                            break;
                    }
                }
            }
            x++;
        });
    }

    /// <summary>
    /// This is the method that is different than the usual traditional printing
    /// It accesses the DPM Printer found @C:\Program Files\Tekla Structures\2020.0\nt\bin\applications\Tekla\Model\DPMPrinter
    /// By setting the "printactive" argument to false it will ask for a dpm file found in the model folder under drawings/snapshots/
    /// It will not set as active a drawing so the Tekla thread will remain unocuppied
    /// </summary>
    /// <param name="drawing"></param>
    /// <returns></returns>
    private static bool PrintSingleDrawing(Drawing drawing)
    {
        string settingsFile = string.Format(@"""{0}""", templateFile);
        string outputfile = string.Format(@"""{3}\{0}-{1}-{2}.pdf""", drawing.Name, drawing.Title3, drawing.Mark, drawingFolder);
        string dpmFile = string.Empty;
        GetDPMFileOfDrawing(drawing, new Model().GetInfo().ModelPath, ref dpmFile);
        dpmFile = $@"""{dpmFile}""";

        if (dpmFile.Equals("\"\"") || dpmFile.IsNullOrEmpty()) { return false; }

        ProcessStartInfo startInfo = new ProcessStartInfo()
        {
            WindowStyle = ProcessWindowStyle.Hidden,
            FileName = BinPath,
            Arguments = string.Format($@"dpm:{dpmFile} printActive:false printer:pdf out:{outputfile} settingsFile:{settingsFile}"),
            RedirectStandardOutput = true,
            UseShellExecute = false
        };

        var proc = new Process
        {
            StartInfo = startInfo
        };

        proc.Start();
        proc.BeginOutputReadLine();
        proc.WaitForExit();
        return true;
    }

    /// <summary>
    /// This is used in case the user ask for the drawing to be printed normally
    /// </summary>
    /// <param name="drawing"></param>
    /// <returns></returns>
    private static bool PrintSingleDrawingTraditional(Drawing drawing)
    {
        try
        {
            string settingsFile = string.Format(@"""{0}""", templateFile);
            string outputfile = string.Format(@"""{3}\{0}-{1}-{2}.pdf""", drawing.Name, drawing.Title3, drawing.Mark, drawingFolder);
            new DrawingHandler().SetActiveDrawing(drawing, false);

            ProcessStartInfo startInfo = new ProcessStartInfo()
            {
                WindowStyle = ProcessWindowStyle.Hidden,
                FileName = BinPath,
                Arguments = string.Format($@"printActive:true printer:pdf out:{outputfile} settingsFile:{settingsFile}"),
                RedirectStandardOutput = true,
                UseShellExecute = false
            };

            var proc = new Process
            {
                StartInfo = startInfo
            };

            proc.Start();
            proc.BeginOutputReadLine();
            proc.WaitForExit();
            new DrawingHandler().CloseActiveDrawing(false);
            return true;
        }
        catch (Exception)
        {
            MessageBox.Show($"Not able to print drawing:{drawing.Mark}-{drawing.Name}-{drawing.Title1}");
            return false;
        }
    }

    /// <summary>
    /// This will retrieve from a dictionary<GUID,metadataFile> the metadata file of the coresponding GUID
    /// </summary>
    /// <param name="drawing"></param>
    /// <param name="modelPath"></param>
    /// <param name="dpmFilePath"></param>
    public static void GetDPMFileOfDrawing(Drawing drawing, string modelPath, ref string dpmFilePath)
    {
        dpmFilePath = $@"{modelPath}\drawings\snapshots\{metadataWithGUIDs[drawing.GetIdentifier().GUID.ToString()]}.dg.DPM";
    }

    /// <summary>
    /// Creates the booklet with it's folder
    /// </summary>
    /// <param name="bookletFolderName"></param>
    /// <param name="targetPath"></param>
    /// <param name="pdfs"></param>
    private static void MergePDFs(string bookletFolderName, string targetPath, List<string> pdfs)
    {
        using (PdfDocument targetDoc = new PdfDocument())
        {
            foreach (string pdf in pdfs)
            {
                using (PdfDocument pdfDoc = PdfReader.Open(pdf, PdfDocumentOpenMode.Import))
                {
                    for (int i = 0; i < pdfDoc.PageCount; i++)
                    {
                        targetDoc.AddPage(pdfDoc.Pages[i]);
                    }
                }
            }
            if (targetDoc.PageCount > 0)
            {
                try
                {
                    FolderCreator(bookletFolderName);
                    targetDoc.Save(targetPath);
                }
                catch (System.IO.DirectoryNotFoundException)
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Log("Booklet creation failed!");
                    Console.ForegroundColor = ConsoleColor.White;
                }
            }
        }
    }

    /// <summary>
    /// From all the selected drawing finds all the drawings with the given name
    /// This coresponds to the Femern rules for creating booklets
    /// </summary>
    /// <param name="drawingList"></param>
    /// <param name="drawingName"></param>
    /// <returns></returns>
    private static List<Drawing> Booklet(List<Drawing> drawingList, string drawingName)
    {
        if (drawingList != null)
        {
            var s = drawingList.FindAll(x => x.Name == drawingName);
            return s;
        }
        else
        {
            return drawingList;
        }
    }

    /// <summary>
    /// Creates a folder with the given name at BookletFolder
    /// </summary>
    /// <param name="bookletFolderName"></param>
    public static void FolderCreator(string bookletFolderName)
    {
        try
        {
            string path = Path.Combine(BookletFolder, bookletFolderName);

            if (!Directory.Exists(path))
            {
                Directory.CreateDirectory(path);
            }
        }
        catch (IOException)
        {
            MessageBox.Show("Path is invalid");
        }
    }

    /// <summary>
    /// Deletes the given files from a List of paths
    /// </summary>
    /// <param name="filesToBeDeleted"></param>
    private static void DeleteFiles(List<string> filesToBeDeleted)
    {
        foreach (var file in from string file in filesToBeDeleted
                             where File.Exists(file)
                             select file)
        {
            try
            {
                File.Delete(file);
            }
            catch (System.IO.IOException)
            {
                DialogResult dialogResult = MessageBox.Show($"Unable to delete file {file}. It is probably used by another process. \n Click Retry to try again or Cancel exit and not delete the files.", "File in use", MessageBoxButtons.RetryCancel);
                if (dialogResult == DialogResult.Retry)
                {
                    DeleteFiles(filesToBeDeleted);
                }
                else if (dialogResult == DialogResult.Cancel) return;
            }
        }
    }

    /// <summary>
    /// In the drawings folder of the model folder looks for all the metadata files and removes the extension
    /// The important part is the name of the metadata files which coresponds to the name of the dpm file
    /// </summary>
    /// <param name="modelPath"></param>
    /// <returns></returns>
    ///
    public static List<string> GetListOfMetadataFilesFromFolder(string modelPath)
    {
        List<string> metaDataFiles;
        String dpmFilesDirectoryPath = $@"{modelPath}\drawings";

        metaDataFiles = Directory.GetFiles(dpmFilesDirectoryPath, "*dg.metadata", SearchOption.TopDirectoryOnly).Select(x => Path.GetFileName(x).Remove(37)).ToList();

        return metaDataFiles;
    }

    /// <summary>
    /// For each metadata file in the drawings folder extracts the GUID from the third row
    /// Checks whether or not the drawings has a snapshot and pairs it with the metadata file name
    /// </summary>
    /// <param name="modelPath"></param>
    /// <returns></returns>
    public static Dictionary<string, string> GetGUIDsFromMetadataFiles(string modelPath)
    {
        Dictionary<string, string> metadataWithGUID = new Dictionary<string, string>();
        Dictionary<string, string> drawingsWithoutSnapshot = new Dictionary<string, string>();
        string folder = $@"{modelPath}\drawings";
        string snapshotFolder = $@"{folder}\snapshots\";
        var metaDataFiles = GetListOfMetadataFilesFromFolder(modelPath);

        metaDataFiles.ForEach(file =>
        {
            string filePath = $@"{folder}\{file}.dg.metadata";
            var lines = File.ReadLines(filePath);
            int lineIndex = 2; // The index of the line to read (0-based)
            var line = lines.ElementAt(lineIndex);
            if (File.Exists($@"{snapshotFolder}\{file}.dg.DPM"))
            {
                metadataWithGUID.Add(file, line);
            }
            else
            {
                drawingsWithoutSnapshot.Add(file, line);
            }
        });

        metadataWithGUID = GetDrawingsWithMultipleVersions(metadataWithGUID, modelPath);
        return metadataWithGUID;
    }

    /// <summary>
    /// Flips the Dictionary and finds the drawings with multiple versions
    /// </summary>
    /// <param name="metadataWithGUID"></param>
    /// <param name="modelPath"></param>
    /// <returns>The dictionary of the drawings with the last version</returns>
    public static Dictionary<string, string> GetDrawingsWithMultipleVersions(Dictionary<string, string> metadataWithGUID, string modelPath)
    {
        Dictionary<string, List<string>> flipped = new Dictionary<string, List<string>>();
        //flips the dictionary of format <metadata,GUID> to <GUID,List<metadata>
        foreach (var key in metadataWithGUID.Keys)
        {
            if (!flipped.TryGetValue(metadataWithGUID[key], out var valueList))
            {
                valueList = new List<string>();
                flipped.Add(metadataWithGUID[key], valueList);
            }
            valueList.Add(key);
        }

        //The dictionary with the last version of the drawings
        Dictionary<string, string> drawings = GetTheLastVersions(flipped, $@"{modelPath}\drawings");
        return drawings;
    }

    /// <summary>
    /// Checks whether or not a drawing has multiple versions
    /// </summary>
    /// <param name="drawings"></param>
    /// <param name="folder"></param>
    /// <returns>A dictionary <GUID,dg.metadata file> for all drawings in the folder</returns>
    public static Dictionary<string, string> GetTheLastVersions(Dictionary<string, List<string>> drawings, string folder)
    {
        List<int> versionDates = new List<int>();
        Dictionary<int, string> versionWithDate = new Dictionary<int, string>();

        Dictionary<string, string> lastVersionWithGUID = new Dictionary<string, string>();
        int x = 0;
        foreach (var drawing in drawings)
        {
            if (drawing.Value.Count > 1)
            {
                versionDates.Clear();
                foreach (var version in drawing.Value)
                {
                    string filePath = $@"{folder}\{version}.dg.metadata";
                    var lines = File.ReadLines(filePath);
                    //The modified date place in the metadata file
                    int.TryParse(lines.ElementAt(50), out int modifyDate);
                    //The created date place in the metadata file
                    int.TryParse(lines.ElementAt(53), out int createDate);
                    //Get the last date, unmodified drawings have modified date set to 0
                    versionDates.Add(Math.Max(modifyDate, createDate) + x);

                    //Matches the drawing with the date
                    versionWithDate.TryAdd(Math.Max(modifyDate, createDate) + x, version);
                    x++;
                }

                //Gets the last date
                int max = System.Linq.Enumerable.Max(versionDates);
                //The version coresponding to the last date

                string lastVersion = versionWithDate[max];

                //last version for each drawing with more than 1 versions

                lastVersionWithGUID.Add(drawing.Key, lastVersion);
            }
            else
            {
                //unchanged for drawings with only one version
                lastVersionWithGUID.Add(drawing.Key, drawing.Value[0]);
            }
        }

        //as Dictionary<GUID, metadata>
        return lastVersionWithGUID;
    }
}