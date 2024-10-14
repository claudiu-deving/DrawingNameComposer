using PdfSharp.Pdf;
using PdfSharp.Pdf.IO;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Input;
using Tekla.Structures.Drawing;
using Tekla.Structures.DrawingInternal;
using Tekla.Structures.Model;

namespace DrawingNameComposer.Services;

public class PrintingSettingsService : IPrintingSettingsService
{
	public IEnumerable<string> Get()
	{
		return [];
	}
}

public class PrintingService
{

	public PrintingService(PrintingSettingsService printingSettingsService)
	{
		_printingSettingsService = printingSettingsService;
	}

	public void PrintSelectedDrawings(string template)
	{
		var selectedDrawings = new DrawingHandler().GetDrawingSelector().GetSelected();
		List<Drawing> drawings = [];
		while (selectedDrawings.MoveNext())
		{
			drawings.Add(selectedDrawings.Current);
			PrintSingleDrawing(selectedDrawings.Current, template);
		}

	}


	private static readonly string _printOption = $@"{Path.Combine(new Model().GetInfo().ModelPath, "attributes", "PdfPrintOptions.xml")}";

	private static string _drawingOutputFolder = $@"{Path.Combine(new Model().GetInfo().ModelPath, "Plotfiles")}";

	private static string _bookletFolder = $@"{Path.Combine(new Model().GetInfo().ModelPath, "Plotfiles", "Booklet")}";

	/// <summary>
	/// Tracks the number of drawings that failed to print
	/// </summary>
	private static int _jobsFailed;

	/// <summary>
	/// Tracks the number of drawing skipped by user
	/// </summary>
	private static int _jobsSkipped;

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
		get => _drawingOutputFolder;
		set { if (value != null || value != string.Empty || !Uri.IsWellFormedUriString(value, UriKind.Absolute)) _drawingOutputFolder = value; }
	}

	/// <summary>
	/// The booklet folder
	/// </summary>
	public static string BookletFolder
	{
		get => _bookletFolder;
		set { if (value != null || value != string.Empty || !Uri.IsWellFormedUriString(value, UriKind.Absolute)) _bookletFolder = value; }
	}

	/// <summary>
	/// The dictionary that ties the drawing to it's most current dg.DPM file
	/// </summary>
	private static readonly Dictionary<string, string> metadataWithGUIDs = GetGUIDsFromMetadataFiles(new Model().GetInfo().ModelPath);
	private readonly PrintingSettingsService _printingSettingsService;

	/// <summary>
	/// Main method
	/// </summary>
	/// <param name="deleteDrawings"></param>
	public static void PrintDrawingCommand(bool deleteDrawings, bool printSnapshot, string template)
	{
		try
		{
			//The double quotation marks are needed to pass the arguments to the DPM Printer Proccess
			string settingsFile = string.Format(@"""{0}""", _printOption);

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

			List<Drawing> alldrawings = [];
			List<string> drawingNames = [];
			foreach (Drawing drawing in drawingsEnum)
			{
				alldrawings.Add(drawing);
				drawingNames.Add(drawing.Name);
			}
			var drawingsCount = alldrawings.Count;
			Log($"{drawingsCount} drawings to be printed.");
			drawingNames = drawingNames.Distinct().ToList();
			int numberOfProcessors = (int)Math.Floor((decimal)Environment.ProcessorCount) / 2;

			var numberOfBooklets = drawingNames.Count;
			//A way to figure out how to handle the printing
			//for example 3 booklets containing : 1, 2, 2 drawings each will print slower
			//by booklets in parallel than printing in parallel all 5 drawings separately
			if (printSnapshot)
			{
				if (numberOfBooklets > numberOfProcessors)
				{
					CreateBookletInParallel(alldrawings, drawingNames, template);
				}
				else
				{
					PrintDrawingsInParallel(alldrawings, template);
				}
			}
			else
			{
				if (numberOfBooklets > numberOfProcessors)
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
			Log($"{_jobsSkipped} drawings skipped.");
			Log($"{_jobsFailed} drawings failed.");
			Log($"{drawingsCount - _jobsSkipped} drawings printed in: {stopwatch.Elapsed.Minutes} min and {stopwatch.Elapsed.Seconds} s");
			Log($"=====================Printing done: {DateTime.Now}===========================");
			Log("");

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
		}
		catch (Exception ex)
		{
		}
	}

	/// <summary>
	/// Sends messages to the console and appends them to a list for printing on file at the end
	/// </summary>
	/// <param name="message"></param>
	public static void Log(string message)
	{
		Console.WriteLine(message);
	}

	/// <summary>
	/// Prints the final log to file
	/// </summary>
	private static void LogFinal()
	{
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
		List<string> pdfs = [];
		drawingNames.ForEach(drawingName =>
		{
			List<Drawing> booklet = Booklet(alldrawings, drawingName);

			booklet.ForEach(drawing =>
			{
				string outputfile = string.Format(@"""{3}\{0}-{1}-{2}.pdf""", drawing.Name, drawing.Title3, drawing.Mark, _drawingOutputFolder);
				string plotName = drawing.GetPlotFileName(false).Replace("COW", "CN3");
				combinefileName = string.Format(@"{1}\{0}\{0}.pdf", plotName, _bookletFolder);
				bookletFolderName = $@"{_bookletFolder}\{plotName}";
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
	private static void CreateBookletInParallel(List<Drawing> alldrawings, List<string> drawingNames, string template)
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
					if (PrintSingleDrawing(drawing, template))
					{
						Console.ForegroundColor = ConsoleColor.Green;
						Log($"Drawing no.{x + 1} - Mark: {booklet[x].Mark} of booklet no.{index + 1} - Name:{drawingName} printed successfully");
					}
					else
					{
						var result = MessageBox.Show($"The drawing: {drawing.Mark}-{drawing.Name}-{drawing.Title1} could not be printed,\nmake sure that the drawing has a valid snapshot.\nIf you wish to print the drawing using the default method press Yes.", "Not able to print drawing", MessageBoxButton.YesNo);
						switch (result)
						{
							case MessageBoxResult.Yes:
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
												_jobsFailed++;
												Log($"Drawing no.{x + 1} - Mark: {booklet[x].Mark} of booklet no.{index + 1} - Name:{drawingName} traditional printing failed.");
											}
											break;
									}
								}
								break;

							case MessageBoxResult.No:
								{
									Log($"Drawing no.{x + 1} - Mark: {booklet[x].Mark} of booklet no.{index + 1} - Name:{drawingName} printing skipped.");
									_jobsSkipped++;
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
					var result = MessageBox.Show($"The drawing: {drawing.Mark}-{drawing.Name}-{drawing.Title1} could not be printed,\nmake sure that the drawing has a valid snapshot.\nIf you wish to print the drawing using the default method press Yes.", "Not able to print drawing", MessageBoxButton.YesNo);
					switch (result)
					{
						case MessageBoxResult.Yes:
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
											_jobsFailed++;
											Log($"Drawing no.{x + 1} - Mark: {booklet[x].Mark} of booklet no.{y + 1} - Name:{drawingName} traditional printing failed.");
										}
										break;
								}
							}
							break;

						case MessageBoxResult.No:
							{
								Log($"Drawing no.{x + 1} - Mark: {booklet[x].Mark} of booklet no.{y + 1} - Name:{drawingName} printing skipped.");
								_jobsSkipped++;
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
	private static void PrintDrawingsInParallel(List<Drawing> booklet, string template)
	{
		ParallelOptions parallelOptions = new ParallelOptions()
		{
			MaxDegreeOfParallelism = (int)Math.Floor((decimal)2 * Environment.ProcessorCount)
		};

		Parallel.ForEach(booklet.AsParallel().AsOrdered(), parallelOptions, (drawing, state, index) =>
		{
			if (drawing != null && drawing.Mark.Trim() != string.Empty)
			{
				drawing.Select();
				Console.ForegroundColor = ConsoleColor.White;
				Log($"Printing drawing no {index + 1}: {booklet[(int)index].Mark} out of {booklet.Count}");
				if (PrintSingleDrawing(drawing, template))
				{
					Console.ForegroundColor = ConsoleColor.Green;
					Log($"Drawing no {index + 1}: {booklet[(int)index].Mark} printed successfully");
				}
				else
				{
					var result = MessageBox.Show($"The drawing: {drawing.Mark}-{drawing.Name}-{drawing.Title1} could not be printed,\nmake sure that the drawing has a valid snapshot.\nIf you wish to print the drawing using the default method press Yes.", "Not able to print drawing", MessageBoxButton.YesNo);
					switch (result)
					{
						case MessageBoxResult.Yes:
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
											_jobsFailed++;
											Console.ForegroundColor = ConsoleColor.Red;
											Log($"Drawing no {index + 1}: {booklet[(int)index].Mark} printed traditionally failed. The drawing might not be up to date.");
										}
										break;
								}
							}
							break;

						case MessageBoxResult.No:
							{
								Console.ForegroundColor = ConsoleColor.White;
								Log($"Drawing no {index + 1}: {booklet[(int)index].Mark} printing skipped.");
								_jobsSkipped++;
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
					var result = MessageBox.Show($"The drawing: {drawing.Mark}-{drawing.Name}-{drawing.Title1} could not be printed,\nmake sure that the drawing has a valid snapshot.\nIf you wish to print the drawing using the default method press Yes.", "Not able to print drawing", MessageBoxButton.YesNo);
					switch (result)
					{
						case MessageBoxResult.Yes:
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
											_jobsFailed++;
											Console.ForegroundColor = ConsoleColor.Red;
											Log($"Drawing no {x + 1}: {booklet[(int)x].Mark} printed traditionally failed. The drawing might not be up to date.");
										}
										break;
								}
							}
							break;

						case MessageBoxResult.No:
							{
								Console.ForegroundColor = ConsoleColor.White;
								Log($"Drawing no {x + 1}: {booklet[(int)x].Mark} printing skipped.");
								_jobsSkipped++;
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
	private static bool PrintSingleDrawing(Drawing drawing, string template, bool printSnapshot = true)
	{
		string settingsFile = string.Format(@"""{0}""", _printOption);
		Directory.CreateDirectory(_drawingOutputFolder);
		var fileName = drawing.GetDrawingFileName(template);
		string outputfile = string.Format(@"""{0}.pdf""", Path.Combine(_drawingOutputFolder, fileName));
		string settingsFileArgument = $"settingsFile:{settingsFile}";
		string dpmFile = string.Empty;
		GetDPMFileOfDrawing(drawing, new Model().GetInfo().ModelPath, ref dpmFile);
		string dpmFileArgument;
		if (string.IsNullOrEmpty(dpmFile))
		{
			dpmFileArgument = string.Empty;
			printSnapshot = false;
		}
		else
		{
			dpmFile = $@"""{dpmFile}""";
			dpmFileArgument = $"dpm:{dpmFile} ";
		}
		if (!File.Exists(settingsFile))
		{
			settingsFileArgument = string.Empty;
		}
		string arguments;
		if (!printSnapshot && drawing.UpToDateStatus == DrawingUpToDateStatus.DrawingIsUpToDate)
		{
			new DrawingHandler().SetActiveDrawing(drawing, false);
		}

		arguments = string.Format($@"{dpmFileArgument} printActive:{!printSnapshot} printer:pdf out:{outputfile} {settingsFileArgument}");

		ProcessStartInfo startInfo = new ProcessStartInfo()
		{
			WindowStyle = ProcessWindowStyle.Normal,
			FileName = BinPath,
			Arguments = arguments,
			RedirectStandardOutput = true,
			RedirectStandardError = true,
			UseShellExecute = false
		};

		var proc = new Process
		{
			StartInfo = startInfo
		};

		var processStarted = proc.Start();
		if (!processStarted)
		{
			return false;
		}
		if (!printSnapshot)
		{

			new DrawingHandler().CloseActiveDrawing(true);
		}
		proc.BeginOutputReadLine();
		proc.WaitForExit();
		return true;
	}

	/// <summary>
	/// This is used in case the user ask for the drawing to be printed normally
	/// </summary>
	/// <param name="drawing"></param>
	/// <returns></returns>
	private static bool PrintSingleDrawingTraditional(Drawing drawing, string? template = null)
	{
		try
		{
			string outputfile = string.Empty;
			string settingsFile = string.Format(@"""{0}""", _printOption);
			if (template == null)
			{
				outputfile = string.Format(@"""{3}\{0}-{1}-{2}.pdf""", drawing.Name, drawing.Title3, drawing.Mark, _drawingOutputFolder);
			}
			else
			{
				Directory.CreateDirectory(_drawingOutputFolder);
				var fileName = drawing.GetDrawingFileName(template);
				outputfile = string.Format(@"""{0}.pdf""", Path.Combine(_drawingOutputFolder, fileName));
			}
			new DrawingHandler().SetActiveDrawing(drawing, false);

			ProcessStartInfo startInfo = new()
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
		var key = drawing.GetIdentifier().GUID.ToString();
		if (metadataWithGUIDs.ContainsKey(key))
		{
			dpmFilePath = $@"{modelPath}\drawings\snapshots\{metadataWithGUIDs[key]}.dg.DPM";
		}
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
				MessageBoxResult dialogResult = MessageBox.Show($"Unable to delete file {file}. It is probably used by another process. \n Click Yes to try again or Cancel exit and not delete the files.", "File in use", MessageBoxButton.YesNoCancel);
				if (dialogResult == MessageBoxResult.Yes)
				{
					DeleteFiles(filesToBeDeleted);
				}
				else if (dialogResult == MessageBoxResult.Cancel) return;
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
		Dictionary<string, string> metadataWithGUID = [];
		Dictionary<string, string> drawingsWithoutSnapshot = [];
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
		Dictionary<string, List<string>> flipped = [];
		//flips the dictionary of format <metadata,GUID> to <GUID,List<metadata>
		foreach (var key in metadataWithGUID.Keys)
		{
			if (!flipped.TryGetValue(metadataWithGUID[key], out var valueList))
			{
				valueList = [];
				if (!flipped.ContainsKey(key))
				{
					flipped.Add(metadataWithGUID[key], valueList);
				}
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
		List<int> versionDates = [];
		Dictionary<int, string> versionWithDate = [];

		Dictionary<string, string> lastVersionWithGUID = [];
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
					var key = Math.Max(modifyDate, createDate) + x;
					//Matches the drawing with the date
					if (!versionWithDate.ContainsKey(key))
					{
						versionWithDate.Add(key, version);
					}
					x++;
				}

				//Gets the last date
				int max = System.Linq.Enumerable.Max(versionDates);
				//The version coresponding to the last date

				string lastVersion = versionWithDate[max];

				//last version for each drawing with more than 1 versions
				if (!lastVersionWithGUID.ContainsKey(drawing.Key))
				{
					lastVersionWithGUID.Add(drawing.Key, lastVersion);
				}
			}
			else
			{
				//unchanged for drawings with only one version
				if (!lastVersionWithGUID.ContainsKey(drawing.Key))
				{
					lastVersionWithGUID.Add(drawing.Key, drawing.Value[0]);
				}
			}
		}

		//as Dictionary<GUID, metadata>
		return lastVersionWithGUID;
	}
}
