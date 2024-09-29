using FFMpegCore;
using Mscc.GenerativeAI;
using System.Data;
using System.Text;
using YoutubeDLSharp;
using YoutubeDLSharp.Metadata;

namespace VideoToText
{
    public partial class Form1 : Form
    {
        private CancellationTokenSource cancellationTokenSource;
        //private const string FFMpegPath = "D:\\Testing Youtube Subtitle - FFmpeg\\ffmpeg-v5\\bin";
        //private const string YTDLPath = "D:\\Testing Youtube Subtitle - FFmpeg\\ffmpeg-v5\\bin\\yt-dlp.exe";

        private static readonly string FFMpegPath = Path.Combine(Environment.CurrentDirectory, "bin");
        private static readonly string YTDLPath = Path.Combine(Environment.CurrentDirectory, "bin\\yt-dlp.exe");
        //private const string YTDLPath = "D:\\Testing Youtube Subtitle - FFmpeg\\ffmpeg-v5\\bin\\yt-dlp.exe";

        //private const string TemporaryFolder = "D:\\Testing Youtube Subtitle - FFmpeg\\ffmpeg-v5\\tmp";
        //private const string initialPrompt = "Tôi là một Phật tử theo truyền thống Phật giáo Nguyên thủy Theravada đang ghi chép bài giảng của Sư. Hãy thực hiện các nhiệm vụ sau với file âm thanh đính kèm: " +
        //                     "1. Chuyển đổi toàn bộ nội dung âm thanh thành văn bản. " +
        //                     "2. Loại bỏ các từ không cần thiết thường xuất hiện trong văn nói như từ đệm, từ lặp, hoặc các cụm từ trong văn nói." +
        //                     "3. Sắp xếp và định dạng lại nội dung thành các đoạn văn có cấu trúc rõ ràng, mạch lạc. " +
        //                     "4. Đảm bảo kết quả cuối cùng dễ đọc và dễ hiểu nhất có thể, trong khi vẫn giữ nguyên ý nghĩa chính của nội dung gốc. " +
        //                     "5. Nếu có bất kỳ thuật ngữ hoặc khái niệm nào liên quan đến Phật giáo Nguyên thủy Theravada, hãy chú ý giữ nguyên và sử dụng chính xác.";

        //private const int MaxOutputTokens = 8192;



        private const int MaxOutputTokens = 8192;

        // Declare class-level variables for IGenerativeAI and model
        private IGenerativeAI generativeAI;
        private GenerativeModel model;


        private void InitializeGenerativeAI()
        {
            string geminiAiApiKey = apiKeyTextBox.Text.Trim(); // Get the API key from the text box

            // Check if API key is valid
            if (string.IsNullOrEmpty(geminiAiApiKey))
            {
                MessageBox.Show("Please enter API Key.");
                return;
            }

            // Initialize the generative AI client with the provided API key
            generativeAI = new GoogleAI(geminiAiApiKey);

            // Set up the generative model with the selected model name and configuration
            var modelConfig = new GenerationConfig
            {
                MaxOutputTokens = MaxOutputTokens
            };

            // Create the generative model
            model = generativeAI.GenerativeModel(modelComboBox.Text, modelConfig);

            // Set a timeout for the model operation
            model.Timeout = TimeSpan.FromMinutes(10);
        }

        private async void btnConvertVideoToText_Click(object sender, EventArgs e)
        {
            btnConvertVideoToText.Enabled = false;
            try
            {
                cancellationTokenSource = new CancellationTokenSource();

                string youtubeApiKey = "AIzaSyA4HGKpLTz76bL-xaXKCzbu9DGI1YtSJVA";

                // Get the current directory
                string currentDirectory = Environment.CurrentDirectory;

                // Specify output paths for downloaded audio and converted MP3 files relative to the current directory
                string downloadedAudiosPath = Path.Combine(currentDirectory, "downloaded_audios");
                string convertedAudiosOutputPath = Path.Combine(currentDirectory, "converted_audios");

                // Create the directories if they do not exist (this method does nothing if the directory already exists)
                Directory.CreateDirectory(downloadedAudiosPath);
                Directory.CreateDirectory(convertedAudiosOutputPath);

                string[] urls = GetUrls();
                if (urls.Length == 0)
                {
                    string urlType = radioPlaylist.Checked ? "Playlist URL(s)" : "Video URL(s)";
                    MessageBox.Show($"Please enter {urlType}.");
                    return;
                }

                // Get the start and end index from the NumericUpDown controls
                int startIndex = (int)numericUpDownStart.Value;
                int endIndex = (int)numericUpDownEnd.Value;

                // Validate the indices
                if (startIndex < 0 || endIndex < 0 || startIndex > endIndex)
                {
                    MessageBox.Show("Please enter valid indices: Start should be less than or equal to End and both should be non-negative.");
                    return;
                }

                // Simulate processing a range of data (for example, from an array)
                string[] data = { "Item1", "Item2", "Item3", "Item4", "Item5" };



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
                    foreach (var filePath in Directory.GetFiles(convertedAudiosOutputPath))
                    {
                        string fileName = Path.GetFileNameWithoutExtension(filePath);
                        string outputFilePath = Path.Combine(mp3ToTextOutputPath, $"{fileName}.txt");

                        // Check if the output MP3 file already exists, if not, then convert
                        if (!File.Exists(outputFilePath))
                        {
                            tasks.Add(ConvertMp3ToText(model, filePath, outputFilePath, cancellationTokenSource.Token));
                        }
                    }

                    await Task.WhenAll(tasks);


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

                        //var videoUrls = playlistData.Entries.Select(x => x.Url).ToArray();

                        await ProcessVideos(playlistData.Entries.ToArray(), playlistFolder, urlCache, cancellationTokenSource.Token);


                        string convertedAudiosForPlaylistOutputPath = Path.Combine(convertedAudiosOutputPath, playlistData.Title);
                        await ConvertToMp3(playlistFolder, convertedAudiosForPlaylistOutputPath);

                        AppendLog("CONVERT TO MP3 COMPLETE!!!");

                        // ----------- CONVERT MP3 TO TEXT -----------

                        var tasks = new List<Task>();
                        foreach (var filePath in Directory.GetFiles(convertedAudiosForPlaylistOutputPath))
                        {
                            string fileName = Path.GetFileNameWithoutExtension(filePath);
                            string outputFilePath = Path.Combine(mp3ToTextOutputPath, $"{fileName}.txt");

                            // Check if the output MP3 file already exists, if not, then convert
                            if (!File.Exists(outputFilePath))
                            {
                                tasks.Add(ConvertMp3ToText(model, filePath, outputFilePath, cancellationTokenSource.Token));
                            }
                        }

                        await Task.WhenAll(tasks);


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
                            AppendLog(errorMessage); // Log the error
                            throw new InvalidOperationException(errorMessage); // Throw an exception
                        }
                        else
                        {
                            AppendLog($"Successfully fetched data for URL: {url}");
                            urlCache[url] = result; // Cache the result
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
                // Log the error and rethrow it
                AppendLog($"Error: {ex.Message}");
                throw; // Rethrow the original exception
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

                //// Check the cache first
                //if (!urlCache.TryGetValue(videoUrl, out VideoData video))
                //{
                //    // Fetch video data from YouTube
                //    video = (await ytdl.RunVideoDataFetch(videoUrl))?.Data;

                //    // Cache the fetched video data
                //    if (video != null)
                //    {
                //        urlCache[videoUrl] = video; // Cache the result
                //    }
                //    else
                //    {
                //        AppendLog($"Failed to fetch data for URL: {videoUrl}");
                //        return; // Early return if video data is not available
                //    }
                //}

                // Check if the output MP3 file already exists, if not, then convert
                if (Directory.GetFiles(outputDirectory).Any(x => x.Contains(video.ID)))
                {
                    AppendLog($"{video.Title} already exists.");
                    return;
                }

                AppendLog($"Downloading: {video.Title}");

                var progressHandler = new Progress<DownloadProgress>(p => AppendLog($"{video.Title}: {p.Progress * 100:F2}%"));

                var result = await ytdl.RunAudioDownload(video.Url, ct: cancellationToken, progress: progressHandler);

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

            // Check if output folder exists, create if it doesn't
            if (!Directory.Exists(outputFolder))
            {
                Directory.CreateDirectory(outputFolder);
            }

            // Loop through each file in the subfolder and convert to MP3
            var tasks = new List<Task>();
            foreach (var file in Directory.GetFiles(inputFolder))
            {
                string fileName = Path.GetFileNameWithoutExtension(file);
                string outputFilePath = Path.Combine(outputFolder, $"{fileName}.mp3");

                // Check if the output MP3 file already exists, if not, then convert
                if (!File.Exists(outputFilePath))
                {
                    tasks.Add(ConvertToMp3Async(file, outputFilePath));
                }
            }

            // Wait for all conversion tasks to complete
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
                AppendLog($"Starting conversion of MP3 to text for file: '{fileName}'.");

                using (var writer = new StreamWriter(outputPath, append: false, Encoding.UTF8))
                {
                    while (true)
                    {
                        var responseStream = model.GenerateContentStream(request, cancellationToken: cancellationToken);
                        StringBuilder responseBuilder = new();
                        int tokenCount = 0;

                        await foreach (var response in responseStream)
                        {
                            if (response.Text == null)
                            {
                                writer.Close(); // Close the writer before renaming the file
                                throw new InvalidOperationException("Failed to retrieve valid response.");
                            }

                            // Append log, build response, and track token count
                            AppendLogForAi(response.Text);
                            responseBuilder.Append(response.Text);
                            tokenCount += response?.UsageMetadata?.CandidatesTokenCount ?? 0;

                            // Write the streaming output to the file
                            await writer.WriteAsync(response.Text);
                            writer.Flush(); // Ensure data is written to the file immediately
                        }

                        if (tokenCount < MaxOutputTokens)
                            break;

                        // Continue processing if the output reached the token limit
                        request.AddContent(new Content(responseBuilder.ToString()) { Role = Role.Model });
                        index++;

                        request.AddContent(new Content("Viết tiếp tục đến hết phần còn lại") { Role = Role.User });
                        index++;
                    }

                    AppendLog($"Completed conversion of MP3 to text for file: '{fileName}'. \r\nOutput saved at: '{outputPath}'.");
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
                        AppendLog($"Conversion failed for file: '{fileName}'. \r\nOutput saved at: '{failedOutputPath}'.");
                    }
                }
                catch (Exception renameEx)
                {
                    AppendLog($"Failed to rename output file after error: {renameEx.Message}");
                }
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
    }

    public class VideoInfo
    {
        public string Id { get; set; }
        public string Title { get; set; }
    }
}
