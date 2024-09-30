using FFMpegCore;
using Google.Apis.YouTube.v3.Data;
using Mscc.GenerativeAI;
using System.Data;
using System.Diagnostics;
using System.Text;
using System.Threading;
using YoutubeDLSharp;
using YoutubeDLSharp.Metadata;

namespace VideoToText
{
    public partial class Form1 : Form
    {
        private CancellationTokenSource cancellationTokenSource;

        private static readonly string FFMpegPath = Path.Combine(Environment.CurrentDirectory, "bin");
        private static readonly string YTDLPath = Path.Combine(Environment.CurrentDirectory, "bin\\yt-dlp.exe");

        private const int MaxOutputTokens = 8192;

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

            // Set a timeout for the model operation
            model.Timeout = TimeSpan.FromMinutes(10);

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

                    rpmLimit = 1; // The theoretical limit is 2 RPM, but a safer, more realistic value is 1 RPM
                }
            }

            // Initialize RPM timer
            //rpmTimer = new System.Threading.Timer(ResetRpmCounter, null, TimeSpan.Zero, TimeSpan.FromMinutes(1));
        }

        //private void ResetRpmCounter(object state)
        //{
        //    Interlocked.Exchange(ref rpmCounter, 0);
        //}

        private async void btnConvertVideoToText_Click(object sender, EventArgs e)
        {
            btnConvertVideoToText.Enabled = false;
            try
            {
                cancellationTokenSource = new CancellationTokenSource();

                string youtubeApiKey = "AIzaSyA4HGKpLTz76bL-xaXKCzbu9DGI1YtSJVA";

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

                    await ProcessVideos(urlCache.Values.ToArray(), downloadedAudiosPath, urlCache, cancellationTokenSource.Token);

                    await ConvertToMp3(downloadedAudiosPath, convertedAudiosOutputPath);

                    AppendLog("CONVERT TO MP3 COMPLETE!!!");

                    // ----------- CONVERT MP3 TO TEXT -----------

                    var tasks = new List<Task>();


                    //var filteredFiles = Directory.GetFiles(convertedAudiosOutputPath)
                    //                             .Where(file => urls.ToList().Exists(url => file.Contains(url)))
                    //                             .ToList();


                    var selectedIds = urlCache.Where(x => urls.Contains(x.Key))
                        .Select(x => x.Value)
                        .Select(x => x.ID)
                        .ToList();

                    var filteredFiles = Directory.GetFiles(convertedAudiosOutputPath)
                                                 .Where(file => selectedIds.Exists(id => file.Contains(id)))
                                                 .ToList();

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

                    int numberOfBatches = (int)Math.Ceiling((double)filteredFiles.Count / rpmLimit);

                    for (int i = 0; i < numberOfBatches; i++)
                    {
                        var stopwatch = new Stopwatch(); // Start tracking time
                        stopwatch.Start();

                        var currentFilesBatch = filteredFiles.Skip(i * rpmLimit).Take(rpmLimit);

                        foreach (var filePath in currentFilesBatch)
                        {
                            string fileName = Path.GetFileNameWithoutExtension(filePath);
                            string outputFilePath = Path.Combine(mp3ToTextOutputPath, $"{fileName}.txt");

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

                    if (urls.Length == 1 && (startIndex >= urlCache.Values.ToList()[0].Entries.Length
                        || endIndex >= urlCache.Values.ToList()[0].Entries.Length))
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

                        string playlistFolder = Path.Combine(downloadedAudiosPath, playlistData.Title);
                        Directory.CreateDirectory(playlistFolder);

                        // example startIndex = 1, endIndex = 2 ==> ouput = 2
                        var selectedEntries = playlistData.Entries
                            .Skip(startIndex)
                            .Take(endIndex - startIndex + 1)
                            .ToArray();

                        await ProcessVideos(selectedEntries, playlistFolder, urlCache, cancellationTokenSource.Token);

                        string convertedAudiosForPlaylistOutputPath = Path.Combine(convertedAudiosOutputPath, playlistData.Title);
                        await ConvertToMp3(playlistFolder, convertedAudiosForPlaylistOutputPath);

                        AppendLog("CONVERT TO MP3 COMPLETE!!!");

                        // ----------- CONVERT MP3 TO TEXT -----------
                        // using Batch Size
                        var tasks = new List<Task>();

                        var selectedIds = selectedEntries.Select(entry => entry.ID).ToList();

                        var filteredFiles = Directory.GetFiles(convertedAudiosForPlaylistOutputPath)
                                                     .Where(file => selectedIds.Exists(id => file.Contains(id)))
                                                     .ToList();

                        int numberOfBatches = (int)Math.Ceiling((double)filteredFiles.Count / rpmLimit);

                        for (int i = 0; i < numberOfBatches; i++)
                        {
                            var stopwatch = new Stopwatch(); // Start tracking time
                            stopwatch.Start();

                            var currentFilesBatch = filteredFiles.Skip(i * rpmLimit).Take(rpmLimit);

                            foreach (var filePath in currentFilesBatch)
                            {
                                string fileName = Path.GetFileNameWithoutExtension(filePath);
                                string outputFilePath = Path.Combine(mp3ToTextOutputPath, $"{fileName}.txt");

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
                foreach (var url in urls)
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
                }
            }
            catch (Exception ex)
            {
                AppendLog($"Error: {ex.Message}");
                throw;
            }
        }

        private void btnCancel_Click(object sender, EventArgs e)
        {
            cancellationTokenSource?.Cancel();
            AppendLog("\r\nCANCELLED!!!");
        }

        private async Task ProcessVideos(VideoData[] videos, string outputDirectory, Dictionary<string, VideoData> urlCache, CancellationToken cancellationToken)
        {
            var tasks = videos.Select((video) => ProcessSingleVideo(video, outputDirectory, urlCache, cancellationToken));
            await Task.WhenAll(tasks);
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

                var progressHandler = new Progress<DownloadProgress>(p => AppendLog($"{video.Title}: {p.Progress * 100:F2}%"));

                var result = await ytdl.RunAudioDownload($"https://www.youtube.com/watch?v={video.ID}", ct: cancellationToken, progress: progressHandler);

                AppendLog(result.Success ? $"Downloaded: {video.Title}" : $"Failed to download: {video.Title}");
            }
            catch (Exception ex)
            {
                AppendLog($"Error: {ex.Message}");
            }
        }


        private async Task ConvertToMp3(string inputFolder, string outputFolder)
        {
            GlobalFFOptions.Configure(new FFOptions { BinaryFolder = FFMpegPath });

            if (!Directory.Exists(outputFolder))
            {
                Directory.CreateDirectory(outputFolder);
            }

            var tasks = new List<Task>();
            foreach (var file in Directory.GetFiles(inputFolder))
            {
                string fileName = Path.GetFileNameWithoutExtension(file);
                string outputFilePath = Path.Combine(outputFolder, $"{fileName}.mp3");

                if (!File.Exists(outputFilePath))
                {
                    tasks.Add(ConvertToMp3Async(file, outputFilePath));
                }
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
                        AppendLog($"Converted to MP3 -- {Path.GetFileName(inputFilePath)}: {percentage}%");
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

                using (var writer = new StreamWriter(outputPath, append: false, Encoding.UTF8))
                {
                    while (true)
                    {
                        //// Respect RPM limit
                        //while (Interlocked.Increment(ref rpmCounter) > rpmLimit)
                        //{
                        //    await Task.Delay(TimeSpan.FromSeconds(1), cancellationToken);
                        //}
                        //Interlocked.Increment(ref rpmCounter);

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
                                writer.Close();
                                throw new InvalidOperationException("Failed to retrieve valid response.");
                            }

                            AppendLogForAi(response.Text);
                            responseBuilder.Append(response.Text);
                            tokenCount += response?.UsageMetadata?.CandidatesTokenCount ?? 0;

                            await writer.WriteAsync(response.Text);
                            writer.Flush();
                        }

                        if (tokenCount < MaxOutputTokens)
                            break;

                        stopwatch.Stop(); // End tracking time

                        // If the batch took less than 1 minute, wait until the 1 minute mark
                        var timeElapsed = stopwatch.ElapsedMilliseconds;
                        var oneMinuteInMilliseconds = 60 * 1000;
                        if (rpmCounter == rpmLimit && timeElapsed < oneMinuteInMilliseconds)
                        {
                            //var delayTime = oneMinuteInMilliseconds - (int)timeElapsed;
                            //await Task.Delay(delayTime, cancellationToken);
                            var delayTime = oneMinuteInMilliseconds - (int)timeElapsed;

                            // Convert delayTime to seconds, rounding up to ensure we wait enough
                            int delayTimeInSeconds = (int)Math.Ceiling(delayTime / 1000.0);

                            // Log the waiting time in 1-second intervals
                            for (int i1 = 0; i1 < delayTimeInSeconds; i1++)
                            {
                                AppendLog($"Pausing for {delayTimeInSeconds - i1} seconds due to reaching the {theoreticalRpmLimit} RPM (requests per minute) limit...");

                                await Task.Delay(1000, cancellationTokenSource.Token);
                            }

                            // reset request total
                            Interlocked.Exchange(ref rpmCounter, 0);
                        }

                        request.AddContent(new Content(responseBuilder.ToString()) { Role = Role.Model });
                        index++;

                        request.AddContent(new Content("Viết tiếp tục đến hết phần còn lại") { Role = Role.User });
                        index++;

                        Interlocked.Increment(ref rpmCounter);
                        //requestTotal++;
                    }

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
    }

    public class VideoInfo
    {
        public string Id { get; set; }
        public string Title { get; set; }
    }
}
