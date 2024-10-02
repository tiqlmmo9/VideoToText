using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using FFMpegCore;
using Mscc.GenerativeAI;
using System.Data;
using System.Diagnostics;
using System.Text;
using YoutubeDLSharp;
using YoutubeDLSharp.Metadata;
using Body = DocumentFormat.OpenXml.Wordprocessing.Body;
using Color = DocumentFormat.OpenXml.Wordprocessing.Color;
using Path = System.IO.Path;

namespace VideoToText
{
    public partial class Form1 : Form
    {
        private CancellationTokenSource cancellationTokenSource;

        private static readonly string FFMpegPath = Path.Combine(Environment.CurrentDirectory, "bin");
        private static readonly string YTDLPath = Path.Combine(Environment.CurrentDirectory, "bin\\yt-dlp.exe");

        private const int MaxOutputTokens = 8192;
        private const int BatchSize = 20;

        // Declare class-level variables for IGenerativeAI and model
        private IGenerativeAI generativeAI;
        private GenerativeModel model;
        // Semaphore to limit the number of concurrent tasks
        //private SemaphoreSlim semaphore;

        // Timer to respect RPM limits
        //private System.Threading.Timer rpmTimer;
        private int rpmLimit;
        private int rpmCounter;

        private int theoreticalRpmLimit;

        //private int batchSize;
        //private int timerIntervalMilliseconds = 60_000;

        private void InitializeGenerativeAI()
        {
            string geminiAiApiKey = apiKeyTextBox.Text.Trim();

            // Check if API key is valid
            if (string.IsNullOrEmpty(geminiAiApiKey))
            {
                MessageBox.Show("Please enter API Key.");
                return;
            }

            generativeAI = new GoogleAI(geminiAiApiKey);

            var modelConfig = new GenerationConfig
            {
                MaxOutputTokens = MaxOutputTokens
            };

            // Create the generative model
            model = generativeAI.GenerativeModel(modelComboBox.Text, modelConfig);
        }

        //private void ResetRpmCounter(object state)
        //{
        //    Interlocked.Exchange(ref rpmCounter, 0);
        //}

        private async void btnConvertVideoToText_Click(object sender, EventArgs e)
        {
            if (model == null)
            {
                InitializeGenerativeAI();
            }

            var isPayAsYouGo = payAsYouGoCheckBox.Checked;

            // Initialize the semaphore based on the selected model
            if (modelComboBox.Text == Model.Gemini15Flash002)
            {
                //semaphore = new SemaphoreSlim(2000, 2000); // 2,000 RPM for flash Pay-as-you-go
                //rpmLimit = 2000;

                //semaphore = new SemaphoreSlim(15, 15); // 15 RPM for flash free
                //rpmLimit = 15;

                //semaphore = new SemaphoreSlim(10, 10);
                theoreticalRpmLimit = 15;

                rpmLimit = 10; // The theoretical limit is 15 RPM, but a safer, more realistic value is 10 RPM
            }
            else if (modelComboBox.Text == Model.Gemini15Pro002)
            {
                if (isPayAsYouGo)
                {
                    //semaphore = new SemaphoreSlim(1000, 1000); // 1,000 RPM for pro Pay-as-you-go
                    //rpmLimit = 1000;

                    //semaphore = new SemaphoreSlim(10, 10);
                    theoreticalRpmLimit = 1000;

                    rpmLimit = 100;
                }
                else
                {
                    //semaphore = new SemaphoreSlim(2, 2); // 2 RPM for pro free
                    theoreticalRpmLimit = 2;

                    rpmLimit = 2; // The theoretical limit is 2 RPM, but a safer, more realistic value is 1 RPM
                }
            }

            // Initialize RPM timer
            //rpmTimer = new System.Threading.Timer(ResetRpmCounter, null, TimeSpan.Zero, TimeSpan.FromMinutes(1));

            btnConvertVideoToText.Enabled = false;
            try
            {
                cancellationTokenSource = new CancellationTokenSource();

                // Get the current directory
                string currentDirectory = Environment.CurrentDirectory;

                string downloadedAudiosPath = Path.Combine(currentDirectory, "downloaded_audios");
                string convertedAudiosOutputPath = Path.Combine(currentDirectory, "converted_audios");

                Directory.CreateDirectory(downloadedAudiosPath);
                Directory.CreateDirectory(convertedAudiosOutputPath);

                string[] urls = GetUrls();
                if (urls.Length == 0)
                {
                    string urlType = radioPlaylist.Checked ? "Playlist URL(s)" : "Video URL(s)";
                    MessageBox.Show($"Please enter {urlType}.");
                    return;
                }

                int startIndex = (int)numericUpDownStart.Value - 1; // Adjust for 0-based index
                int endIndex = (int)numericUpDownEnd.Value - 1; // Adjust for 0-based index

                if (startIndex < 0 || endIndex < 0 || startIndex > endIndex)
                {
                    MessageBox.Show("Please enter valid indices: Start should be less than or equal to End and both should be non-negative.");
                    return;
                }

                string mp3ToTextOutputPath = outputPathTextBox.Text.Trim();
                if (string.IsNullOrEmpty(mp3ToTextOutputPath))
                {
                    MessageBox.Show("Please enter Output path.");
                    return;
                }

                string geminiAiApiKey = apiKeyTextBox.Text.Trim();
                if (string.IsNullOrEmpty(geminiAiApiKey))
                {
                    MessageBox.Show("Please enter API Key.");
                    return;
                }

                var ytdl = new YoutubeDL
                {
                    YoutubeDLPath = YTDLPath,
                    FFmpegPath = FFMpegPath,
                    OutputFolder = downloadedAudiosPath,
                    //OutputFileTemplate = "%(title)s.%(ext)s"
                };

                var urlCache = new Dictionary<string, VideoData>();

                if (radioVideo.Checked)
                {
                    await ValidateUrls(ytdl, urls, urlCache);

                    // ----------- DOWNLOAD AUDIO -----------

                    var selectedVideos = urlCache
                        .Where(x => urls.Contains(x.Key))
                        .Select(x => x.Value)
                        .ToList();

                    await ProcessVideos(selectedVideos, downloadedAudiosPath, urlCache, cancellationTokenSource.Token);

                    // ----------- CONVERT AUDIO TO MP3 -----------


                    var selectedIds = urlCache.Where(x => urls.Contains(x.Key))
                        .Select(x => x.Value)
                        .Select(x => x.ID)
                        .ToList();

                    var filteredDownloadedFiles = Directory.GetFiles(downloadedAudiosPath)
                                                 .Where(file => selectedIds.Exists(id => file.Contains(id)))
                                                 .ToList();

                    //await ConvertToMp3(downloadedAudiosPath, convertedAudiosOutputPath);
                    await ConvertToMp3(filteredDownloadedFiles, convertedAudiosOutputPath);

                    AppendLog("CONVERT TO MP3 COMPLETE!!!");

                    // ----------- CONVERT MP3 TO TEXT -----------

                    var tasks = new List<Task>();


                    //var filteredFiles = Directory.GetFiles(convertedAudiosOutputPath)
                    //                             .Where(file => urls.ToList().Exists(url => file.Contains(url)))
                    //                             .ToList();


                    //foreach (var filePath in filteredFiles)
                    //{
                    //    string fileName = Path.GetFileNameWithoutExtension(filePath);
                    //    string outputFilePath = Path.Combine(mp3ToTextOutputPath, $"{fileName}.txt");

                    //    // Check if the output MP3 file already exists, if not, then convert
                    //    if (!File.Exists(outputFilePath))
                    //    {
                    //        tasks.Add(ConvertMp3ToText(model, filePath, outputFilePath, cancellationTokenSource.Token));
                    //    }
                    //}

                    //await Task.WhenAll(tasks);

                    var filteredMp3Files = Directory.GetFiles(convertedAudiosOutputPath)
                            .Where(file => selectedIds.Exists(id => file.Contains(id)))
                             .ToList();

                    int numberOfBatches = (int)Math.Ceiling((double)filteredMp3Files.Count / rpmLimit);

                    for (int i = 0; i < numberOfBatches; i++)
                    {
                        var stopwatch = new Stopwatch(); // Start tracking time
                        stopwatch.Start();

                        var currentFilesBatch = filteredMp3Files.Skip(i * rpmLimit).Take(rpmLimit);

                        foreach (var filePath in currentFilesBatch)
                        {
                            string fileName = Path.GetFileNameWithoutExtension(filePath);
                            string outputFilePath = Path.Combine(mp3ToTextOutputPath, $"{fileName}.docx");

                            // Check if the output MP3 file already exists, if not, then convert
                            if (!File.Exists(outputFilePath))
                            {
                                tasks.Add(ConvertMp3ToText(model, filePath, outputFilePath, cancellationTokenSource.Token));
                            }
                        }

                        // Execute the current batch of tasks before moving on to the next batch
                        await Task.WhenAll(tasks);

                        stopwatch.Stop(); // End tracking time

                        // If the batch took less than 1 minute, wait until the 1 minute mark
                        var timeElapsed = stopwatch.ElapsedMilliseconds;
                        var oneMinuteInMilliseconds = 60 * 1000;
                        if (tasks.Count == rpmLimit && timeElapsed < oneMinuteInMilliseconds)
                        {
                            var delayTime = oneMinuteInMilliseconds - (int)timeElapsed;

                            // Convert delayTime to seconds, rounding up to ensure we wait enough
                            int delayTimeInSeconds = (int)Math.Ceiling(delayTime / 1000.0);

                            // Log the waiting time in 1-second intervals
                            for (int i1 = 0; i1 < delayTimeInSeconds; i1++)
                            {
                                AppendLog($"Pausing for {delayTimeInSeconds - i1} seconds due to reaching the {theoreticalRpmLimit} RPM (requests per minute) limit...");

                                await Task.Delay(1000, cancellationTokenSource.Token);
                            }
                        }

                        tasks.Clear(); // Clear the task list for the next batch
                    }


                    AppendLog("\r\nCONVERT MP3 TO TEXT COMPLETE!!!");
                }

                if (radioPlaylist.Checked)
                {
                    await ValidateUrls(ytdl, urls, urlCache);

                    if (urls.Length == 1 && (startIndex >= urlCache.Values.ToList()[0].Entries.Length))
                    //|| endIndex >= urlCache.Values.ToList()[0].Entries.Length)
                    {
                        MessageBox.Show("Start or End index exceeds the number of items.");
                        return;
                    }

                    foreach (var playlistUrl in urls)
                    {
                        if (!urlCache.TryGetValue(playlistUrl, out var playlistData))
                        {
                            playlistData = (await ytdl.RunVideoDataFetch(playlistUrl))?.Data;
                            if (playlistData != null)
                            {
                                urlCache[playlistUrl] = playlistData; // Cache the result
                            }
                        }

                        playlistData.Title = Extensions.RemoveInvalidPathChars(playlistData.Title);
                        // ----------- DOWNLOAD AUDIO -----------

                        string playlistFolder = Path.Combine(downloadedAudiosPath, playlistData.Title);
                        Directory.CreateDirectory(playlistFolder);

                        // example startIndex = 1, endIndex = 2 ==> ouput = 2
                        var selectedVideos = playlistData.Entries
                            .Skip(startIndex)
                            .Take(endIndex - startIndex + 1)
                            .ToList();

                        var selectedIds = selectedVideos.Select(entry => entry.ID).ToList();


                        await ProcessVideos(selectedVideos, playlistFolder, urlCache, cancellationTokenSource.Token);

                        // ----------- CONVERT AUDIO TO MP3 -----------

                        string convertedAudiosForPlaylistOutputPath = Path.Combine(convertedAudiosOutputPath, playlistData.Title);
                        Directory.CreateDirectory(convertedAudiosForPlaylistOutputPath);

                        var filteredDownloadedFiles = Directory.GetFiles(playlistFolder)
                             .Where(file => selectedIds.Exists(id => file.Contains(id)))
                             .ToList();

                        await ConvertToMp3(filteredDownloadedFiles, convertedAudiosForPlaylistOutputPath);

                        AppendLog("CONVERT TO MP3 COMPLETE!!!");

                        // ----------- CONVERT MP3 TO TEXT -----------

                        string playlistMp3ToTextOutputPath = Path.Combine(mp3ToTextOutputPath, playlistData.Title);
                        Directory.CreateDirectory(playlistMp3ToTextOutputPath);

                        var filteredMp3Files = Directory.GetFiles(convertedAudiosForPlaylistOutputPath)
                             .Where(file => selectedIds.Exists(id => file.Contains(id)))
                             .ToList();

                        // using Batch Size
                        var tasks = new List<Task>();

                        int numberOfBatches = (int)Math.Ceiling((double)filteredMp3Files.Count / rpmLimit);

                        for (int i = 0; i < numberOfBatches; i++)
                        {
                            var stopwatch = new Stopwatch(); // Start tracking time
                            stopwatch.Start();

                            var currentFilesBatch = filteredMp3Files.Skip(i * rpmLimit).Take(rpmLimit);

                            foreach (var filePath in currentFilesBatch)
                            {
                                string fileName = Path.GetFileNameWithoutExtension(filePath);
                                string outputFilePath = Path.Combine(playlistMp3ToTextOutputPath, $"{fileName}.docx");

                                // Check if the output MP3 file already exists, if not, then convert
                                if (!File.Exists(outputFilePath))
                                {
                                    tasks.Add(ConvertMp3ToText(model, filePath, outputFilePath, cancellationTokenSource.Token));
                                }
                            }

                            // Execute the current batch of tasks before moving on to the next batch
                            await Task.WhenAll(tasks);

                            stopwatch.Stop(); // End tracking time

                            // If the batch took less than 1 minute, wait until the 1 minute mark
                            var timeElapsed = stopwatch.ElapsedMilliseconds;
                            var oneMinuteInMilliseconds = 60 * 1000;
                            if (tasks.Count == rpmLimit && timeElapsed < oneMinuteInMilliseconds)
                            {
                                var delayTime = oneMinuteInMilliseconds - (int)timeElapsed;

                                // Convert delayTime to seconds, rounding up to ensure we wait enough
                                int delayTimeInSeconds = (int)Math.Ceiling(delayTime / 1000.0);

                                // Log the waiting time in 1-second intervals
                                for (int i1 = 0; i1 < delayTimeInSeconds; i1++)
                                {
                                    AppendLog($"Pausing for {delayTimeInSeconds - i1} seconds due to reaching the {theoreticalRpmLimit} RPM (requests per minute) limit...");

                                    await Task.Delay(1000, cancellationTokenSource.Token);
                                }
                            }

                            tasks.Clear(); // Clear the task list for the next batch
                        }



                        AppendLog("\r\nCONVERT MP3 TO TEXT COMPLETE!!!");
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                btnConvertVideoToText.Enabled = true;
            }
        }

        private async Task ValidateUrls(YoutubeDL ytdl, IEnumerable<string> urls, Dictionary<string, VideoData> urlCache)
        {
            AppendLog($"Validating YouTube URLs...");
            try
            {
                var urlList = urls.ToList();
                int totalBatches = (int)Math.Ceiling((double)urlList.Count / BatchSize);

                for (int i = 0; i < totalBatches; i++)
                {
                    var currentBatch = urlList.Skip(i * BatchSize).Take(BatchSize);

                    var tasks = currentBatch.Select(async url =>
                    {
                        AppendLog($"Validating YouTube URL: {url}");

                        // Check the cache first
                        if (!urlCache.TryGetValue(url, out var result))
                        {
                            result = (await ytdl.RunVideoDataFetch(url))?.Data;
                            if (result == null)
                            {
                                string errorMessage = $"Failed to fetch data for URL: {url}";
                                AppendLog(errorMessage);
                                throw new InvalidOperationException(errorMessage);
                            }
                            else
                            {
                                AppendLog($"Successfully fetched data for URL: {url}");
                                urlCache[url] = result;
                            }
                        }
                        else
                        {
                            AppendLog($"Using cached data for URL: {url}");
                        }
                    });

                    // Await the completion of all tasks in the current batch
                    await Task.WhenAll(tasks);
                }
            }
            catch (Exception ex)
            {
                AppendLog($"Error: {ex.Message}");
                throw ex;
            }
        }

        private void btnCancel_Click(object sender, EventArgs e)
        {
            cancellationTokenSource?.Cancel();
            AppendLog("\r\nCANCELLED!!!");
        }

        private async Task ProcessVideos(List<VideoData> videos, string outputDirectory, Dictionary<string, VideoData> urlCache, CancellationToken cancellationToken)
        {
            var tasks = new List<Task>();

            int numberOfBatches = (int)Math.Ceiling((double)videos.Count / BatchSize);

            for (int i = 0; i < numberOfBatches; i++)
            {
                var currentBatch = videos.Skip(i * BatchSize).Take(BatchSize);

                foreach (var video in currentBatch)
                {
                    tasks.Add(ProcessSingleVideo(video, outputDirectory, urlCache, cancellationToken));
                }

                // Wait for all tasks in the current batch to complete before moving on to the next batch
                await Task.WhenAll(tasks);

                tasks.Clear(); // Clear the task list for the next batch
            }

        }

        private async Task ProcessSingleVideo(VideoData video, string outputDirectory, Dictionary<string, VideoData> urlCache, CancellationToken cancellationToken)
        {
            try
            {
                var ytdl = new YoutubeDL
                {
                    YoutubeDLPath = YTDLPath,
                    FFmpegPath = FFMpegPath,
                    OutputFolder = outputDirectory,
                    //OutputFileTemplate = "%(title)s.%(ext)s"
                };

                if (Directory.GetFiles(outputDirectory).Any(x => x.Contains(video.ID)))
                {
                    AppendLog($"{video.Title} already exists.");
                    return;
                }

                AppendLog($"Downloading: {video.Title}");

                var progressHandler = new Progress<DownloadProgress>(p => AppendLog($"Downloading: {video.Title}: {p.Progress * 100:F2}%"));

                var result = await ytdl.RunAudioDownload($"https://www.youtube.com/watch?v={video.ID}", ct: cancellationToken, progress: progressHandler);

                AppendLog(result.Success ? $"Downloaded: {video.Title}" : $"Failed to download: {video.Title}");
            }
            catch (Exception ex)
            {
                AppendLog($"Error: {ex.Message}");
            }
        }


        private async Task ConvertToMp3(List<string> files, string outputFolder)
        {
            GlobalFFOptions.Configure(new FFOptions { BinaryFolder = FFMpegPath });

            if (!Directory.Exists(outputFolder))
            {
                Directory.CreateDirectory(outputFolder);
            }

            var tasks = new List<Task>();

            //var files = Directory.GetFiles(inputFolder);

            int numberOfBatches = (int)Math.Ceiling((double)files.Count / BatchSize);

            for (int i = 0; i < numberOfBatches; i++)
            {
                var currentBatch = files.Skip(i * BatchSize).Take(BatchSize);

                foreach (var file in currentBatch)
                {
                    string fileName = Path.GetFileNameWithoutExtension(file);
                    string outputFilePath = Path.Combine(outputFolder, $"{fileName}.mp3");

                    if (!File.Exists(outputFilePath))
                    {
                        tasks.Add(ConvertToMp3Async(file, outputFilePath));
                    }
                }

                // Execute the current batch of tasks before moving on to the next batch
                await Task.WhenAll(tasks);

                tasks.Clear(); // Clear the task list for the next batch
            }


            await Task.WhenAll(tasks);
        }

        private async Task ConvertToMp3Async(string inputFilePath, string outputFilePath)
        {
            try
            {
                var analysis = await FFProbe.AnalyseAsync(inputFilePath);

                void OnPercentageProgess(double percentage)
                {
                    if (percentage < 100)
                    {
                        AppendLog($"Converting to MP3 -- {Path.GetFileName(inputFilePath)}: {percentage}%");
                    }
                }

                await FFMpegArguments
                    .FromFileInput(inputFilePath)
                    .OutputToFile(outputFilePath, true, options => options
                        .WithAudioBitrate(48).WithDuration(analysis.Duration))
                    .NotifyOnProgress(OnPercentageProgess, analysis.Duration)
                    .ProcessAsynchronously();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error converting {inputFilePath}: {ex.Message}");
            }
        }

        private string[] GetUrls()
        {
            if (radioPlaylist.Checked)
            {
                return playlistIdTextBox.Text.Split(new[] { Environment.NewLine }, StringSplitOptions.RemoveEmptyEntries);
            }
            if (radioVideo.Checked)
            {
                return videoIdTextBox.Text.Split(new[] { Environment.NewLine }, StringSplitOptions.RemoveEmptyEntries);
            }
            return Array.Empty<string>();
        }

        private async Task ConvertMp3ToText(GenerativeModel model, string inputPath, string outputPath, CancellationToken cancellationToken)
        {
            string fileName = Path.GetFileNameWithoutExtension(inputPath);
            AppendLog($"Uploading file: '{fileName}' started.");

            try
            {
                //await semaphore.WaitAsync(cancellationToken); // Wait for the semaphore

                var uploadMediaResponse = await model.UploadFile(inputPath, cancellationToken: cancellationToken);
                AppendLog($"Upload of file: '{fileName}' completed successfully.");

                var files = await model.ListFiles();
                var audioFile = files.Files.Find(x => x.Name == uploadMediaResponse.File.Name);

                if (files?.Files.Count == 0 || audioFile == null)
                {
                    Console.WriteLine("There is no file for processing!");
                    return;
                }

                var request = new GenerateContentRequest(promptTextBox.Text.Trim());
                request.AddMedia(audioFile); // Add the media file to the request initially

                int index = 1;
                //int requestTotal = 0;
                AppendLog($"Starting conversion of MP3 to text for file: '{fileName}'.");

                using (var wordDocument = WordprocessingDocument.Create(outputPath, WordprocessingDocumentType.Document))
                {
                    // Add a main document part. 
                    MainDocumentPart mainPart = wordDocument.AddMainDocumentPart();

                    // Create the document structure and add some text.
                    mainPart.Document = new Document();
                    Body body = mainPart.Document.AppendChild(new Body());

                    // Add styles part to the document.
                    StyleDefinitionsPart styleDefinitionsPart = mainPart.AddNewPart<StyleDefinitionsPart>();
                    styleDefinitionsPart.Styles = new Styles();

                    // Create a new style (Heading 1) for the file name.
                    Style style = new Style()
                    {
                        Type = StyleValues.Paragraph,
                        StyleId = "Title1",   // ID for the style
                        CustomStyle = true
                    };

                    // Set the name for the style
                    style.Append(new StyleName() { Val = "Title1" });

                    // Set style properties
                    StyleRunProperties styleRunProperties1 = new StyleRunProperties();
                    styleRunProperties1.Append(new RunFonts() { Ascii = "Cambria (Headings)" }); // Font type
                    styleRunProperties1.Append(new FontSize() { Val = "52" });       // Font size in half-points (24 points = 48 half-points)
                    styleRunProperties1.Append(new Color() { Val = "173657" });
                    // Add run properties to the style
                    style.Append(styleRunProperties1);

                    // Append the style to the Styles part
                    styleDefinitionsPart.Styles.Append(style);

                    // Create a paragraph for the file name with Heading1 style
                    Paragraph fileNameParagraph = new Paragraph();
                    ParagraphProperties fileNameParaProperties = new ParagraphProperties();
                    fileNameParaProperties.ParagraphStyleId = new ParagraphStyleId() { Val = "Title1" }; // Apply the heading style

                    fileNameParagraph.Append(fileNameParaProperties); // Apply properties to the paragraph
                    Run fileNameRun = fileNameParagraph.AppendChild(new Run());
                    fileNameRun.AppendChild(new Text(fileName.RemoveAfterBracket())); // Add the fileName text

                    // Append file name paragraph to the body
                    body.AppendChild(fileNameParagraph);

                    Paragraph para = body.AppendChild(new Paragraph());
                    Run run = para.AppendChild(new Run());

                    while (true)
                    {
                        var stopwatch = new Stopwatch(); // Start tracking time
                        stopwatch.Start();

                        var responseStream = model.GenerateContentStream(request, cancellationToken: cancellationToken);
                        Interlocked.Increment(ref rpmCounter);

                        StringBuilder responseBuilder = new();
                        int tokenCount = 0;

                        await foreach (var response in responseStream)
                        {
                            if (response.Text == null)
                            {
                                wordDocument.Dispose();
                                throw new InvalidOperationException("Failed to retrieve valid response.");
                            }

                            AppendLogForAi(response.Text);
                            responseBuilder.Append(response.Text);
                            tokenCount += response?.UsageMetadata?.CandidatesTokenCount ?? 0;

                            run.Append(response.Text);
                        }

                        if (tokenCount < MaxOutputTokens)
                            break;

                        stopwatch.Stop(); // End tracking time

                        var timeElapsed = stopwatch.ElapsedMilliseconds;
                        var oneMinuteInMilliseconds = 60 * 1000;
                        if (rpmCounter == rpmLimit && timeElapsed < oneMinuteInMilliseconds)
                        {
                            var delayTime = oneMinuteInMilliseconds - (int)timeElapsed;
                            int delayTimeInSeconds = (int)Math.Ceiling(delayTime / 1000.0);

                            for (int i1 = 0; i1 < delayTimeInSeconds; i1++)
                            {
                                AppendLog($"Pausing for {delayTimeInSeconds - i1} seconds due to reaching the {theoreticalRpmLimit} RPM (requests per minute) limit...");
                                await Task.Delay(1000, cancellationTokenSource.Token);
                            }

                            Interlocked.Exchange(ref rpmCounter, 0);
                        }

                        request.AddContent(new Content(responseBuilder.ToString()) { Role = Role.Model });
                        index++;

                        request.AddContent(new Content("Viết tiếp tục đến hết phần còn lại") { Role = Role.User });
                        index++;

                        Interlocked.Increment(ref rpmCounter);
                    }

                    //document.Save();
                    AppendLog($"\r\nCompleted conversion of MP3 to text for file: '{fileName}'. \r\nOutput saved at: '{outputPath}'.");
                }


                await model.DeleteFile(audioFile.Name);
            }
            catch (Exception ex)
            {
                AppendLog($"Error during conversion: {ex.Message}");
                // Rename the output file with the "FAILED" suffix
                try
                {
                    if (File.Exists(outputPath))
                    {
                        var failedDirectory = Path.Combine(Path.GetDirectoryName(outputPath), "Failed files");
                        Directory.CreateDirectory(failedDirectory);

                        var failedOutputPath = Path.Combine(failedDirectory, $"{Path.GetFileNameWithoutExtension(outputPath)} - FAILED{Path.GetExtension(outputPath)}");
                        if (File.Exists(failedOutputPath))
                        {
                            File.Delete(failedOutputPath);
                        }
                        File.Move(outputPath, failedOutputPath);
                        AppendLog($"\r\nConversion failed for file: '{fileName}'. \r\nOutput saved at: '{failedOutputPath}'.");

                        // Define the path for the log file (you can customize this path)
                        string logFilePath = Path.Combine(failedOutputPath, "failed_files_log.txt");

                        // Append the file name to the log file
                        using (StreamWriter sw = File.AppendText(logFilePath))
                        {
                            sw.WriteLine($"{DateTime.Now}: Conversion failed for file: '{fileName}'");
                        }
                    }
                }
                catch (Exception renameEx)
                {
                    AppendLog($"\r\nFailed to rename output file after error: {renameEx.Message}");
                }
                //finally
                //{
                //    //await Task.Delay(timerIntervalMilliseconds);
                //    semaphore.Release(); // Release the semaphore
                //}
            }
        }

        private readonly object logLock = new object(); // Lock object

        private void AppendLog(string message)
        {
            if (logTextBox.InvokeRequired)
            {
                logTextBox.Invoke(new Action(() => AppendLog(message)));
            }
            else
            {
                lock (logLock)
                {
                    logTextBox.AppendText(message + Environment.NewLine);
                }
            }
        }

        private void AppendLogForAi(string message)
        {
            if (logTextBox.InvokeRequired)
            {
                logTextBox.Invoke(new Action(() => AppendLog(message)));
            }
            else
            {
                lock (logLock)
                {
                    logTextBox.AppendText(message);
                }
            }
        }

        private void playlistIdTextBox_TextChanged(object sender, EventArgs e)
        {
            string[] urls = GetUrls();

            if (urls.Length > 1)
            {
                numericUpDownStart.Enabled = false;
                numericUpDownEnd.Enabled = false;
                numericUpDownStart.Value = 1;
                numericUpDownEnd.Value = 9999;
            }
            else
            {
                numericUpDownStart.Enabled = true;
                numericUpDownEnd.Enabled = true;
                numericUpDownStart.Value = 1;
                numericUpDownEnd.Value = 9999;
            }
        }

        private void payAsYouGoCheckBox_CheckedChanged(object sender, EventArgs e)
        {
            if (payAsYouGoCheckBox.Checked)
            {
                freeCheckBox.Checked = false;
            }
        }

        private void freeCheckBox_CheckedChanged(object sender, EventArgs e)
        {
            if (freeCheckBox.Checked)
            {
                payAsYouGoCheckBox.Checked = false;
            }
        }

        //private void modelComboBox_SelectedIndexChanged(object sender, EventArgs e)
        //{
        //    if (!string.IsNullOrEmpty(apiKeyTextBox.Text.Trim()))
        //    {
        //        InitializeGenerativeAI();
        //    }
        //}
    }

    public class VideoInfo
    {
        public string Id { get; set; }
        public string Title { get; set; }
    }
}
