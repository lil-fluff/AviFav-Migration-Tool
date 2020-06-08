using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace AvatarConverter {

    internal enum ConnectionResult {
        NONE,
        OK,
        CONNECTION_FAILURE,
        NO_CONNECTION_AVAILABLE
    }

    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window {

        private volatile string apiKey = "";
        private volatile string userAuth = "";

        private volatile string avatarID = "";
        private volatile string avatarData = "";

        private volatile bool isLoggedIn = false, shouldMultiInput = false;

        public MainWindow() {
            InitializeComponent();
            toggleGui(true);
            log("Starting up...");
        }

        private void testBtn_Click(object sender,RoutedEventArgs e) {
            if (fetchApiKey()) {
                testBtn.IsEnabled = false;
                testBtn.Background = new SolidColorBrush(Color.FromRgb(69,149,69));
            }
            else testBtn.Background = new SolidColorBrush(Color.FromRgb(149,69,69));
        }

        private void loginBtn_Click(object sender,RoutedEventArgs e) {
            var hasApiKey = string.IsNullOrWhiteSpace(apiKey) ? fetchApiKey() : true;
            if (hasApiKey) {
                testBtn.IsEnabled = false;
                // Validate username and password fields
                var user = usrnameInputText.Text;
                var pass = passwordInputText.Password;
                if (string.IsNullOrWhiteSpace(user)) log("ERR: Username cannot be blank!");
                else if (string.IsNullOrWhiteSpace(pass)) log("ERR: Password cannot be blank!");
                else if (!(user + pass).Contains(":")) {
                    userAuth = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{user}:{pass}"));
                    if (logInUser()) {
                        log("Logged in successfully!");
                        isLoggedIn = true;
                        toggleGui(false);
                    }
                } else log("ERR: HTTP BASIC Auth does not allow username or password to contain a colon (':') Please check your input and try again.");
            } 
            else log("ERR: VRChat API did not return a valid respone for our apiKey request. Please check your internet connection. (Is VRChat down?)");
        }

        private void fetchBtn_Click(object sender,RoutedEventArgs e) {
            avatarID = aviIdInputText.Text;
            if (shouldMultiInput) {
                getMultiAvatarData().ContinueWith((t) => {
                    if (t.Result) updateOutputText();
                });
            } else if (getSingleAvatarData()) updateOutputText();
        }

        private void updateOutputText() {
            Dispatcher.BeginInvoke(new Action(() => {
                avatarOutputText.Clear();
                avatarOutputText.AppendText(avatarData);
                avatarOutputText.ScrollToEnd();
            }));
        }

        private bool getSingleAvatarData() {
            if (string.IsNullOrWhiteSpace(avatarID)) { log("ERR: Avatar ID was blank!"); return false; }
            if (!Regex.IsMatch(avatarID,"^(avtr_)(\\w{8}-)(\\w{4}-){3}(\\w{12})$")) { log("ERR: Avatar ID pattern is invalid!"); return false; }
            if (isLoggedIn) {
                if (!string.IsNullOrWhiteSpace(apiKey)) {
                    var request = (HttpWebRequest) WebRequest.Create("https://api.vrchat.cloud/api/1/avatars/" + avatarID + "?apiKey=" + apiKey);
                    request.Headers.Add(HttpRequestHeader.Authorization,$"BASIC {userAuth}");
                    request.Timeout = 2500;
                    try {
                        string aviName = "", aviThumb = "";
                        var response = (HttpWebResponse) request.GetResponse();
                        using (StreamReader sr = new StreamReader(response.GetResponseStream())) {
                            var data = sr.ReadToEnd();
                            if (!data.Contains("name")) {
                                avatarOutputText.Clear();
                                avatarOutputText.AppendText(data);
                                return false;
                            }
                            else {
                                var matches = Regex.Matches(data,"(\"name\":\"([^\"]+)\")");
                                if (matches.Count != 0)
                                    aviName = matches[0].Value.Replace("\"","").Replace(":","").Replace("name","").Trim();
                            }
                            if (data.Contains("thumbnailImageUrl")) {
                                var matches = Regex.Matches(data,"(\"thumbnailImageUrl\":\"([^\"]+)\")");
                                if (matches.Count != 0)
                                    aviThumb = matches[0].Value.Replace("\"","").Replace(":","").Replace("thumbnailImageUrl","").Trim();
                            }
                        }
                        if (!string.IsNullOrWhiteSpace(aviName) && !string.IsNullOrWhiteSpace(aviThumb)) {
                            avatarData = "  {\n" +
                                        $"    \"ThumbnailImageUrl\": \"{aviThumb}\",\n" +
                                        $"    \"AvatarID\": \"{avatarID}\",\n" +
                                        $"    \"Name\": \"{aviName}\",\n" +
                                        "  }, \n";
                            return true;
                        } else log("Getting avatar data failed.");
                    } catch (WebException) { }
                } else log("ERR: Missing API key! (Did you log in yet?)");
            } else log("ERR: You must log in to VRCAPI first!");
            return false;
        }

        private async Task<bool> getMultiAvatarData() {
            if (string.IsNullOrWhiteSpace(avatarID)) log("ERR: Avatar ID list was blank!");
            else if (!avatarID.Contains(Environment.NewLine)) log("ERR: Avatar ID input was not in proper list format. Did you mean to use 'single' mode?");
            else if (isLoggedIn) {
                if (!string.IsNullOrWhiteSpace(apiKey)) {
                    var listOfAvatars = Regex.Matches(avatarID,"avtr_\\w{8}-(\\w{4}-){3}\\w{12}");
                    if (listOfAvatars.Count > 0) {
                        var msDelay = listOfAvatars.Count > 100 ? 2500 : listOfAvatars.Count > 50 ? 1500 : listOfAvatars.Count > 20 ? 900 : 650;
                        var res = MessageBox.Show($"Avatars to fetch: {listOfAvatars.Count}\n" +
                                        $"API Delay between requests: {msDelay}ms\n" +
                                        $"Total estimated time to complete: {(int)(listOfAvatars.Count*msDelay/1000)} seconds\n\n" +
                                        $"Please press 'Ok' to continue or 'Cancel' to make changes.", "Ready?",MessageBoxButton.OKCancel,MessageBoxImage.Question);
                        if (res == MessageBoxResult.OK) {
                            toggleGui(true,true);
                            log("Starting API fetch procedure.");
                            Stopwatch st = Stopwatch.StartNew();
                            int sucessful = 0;
                            foreach (Match avi in listOfAvatars) {
                                log($"Waiting {msDelay}ms...");
                                await Task.Delay(msDelay);
                                avatarID = avi.Value;
                                log($"Getting '{avatarID}' data...");
                                var request = (HttpWebRequest) WebRequest.Create("https://api.vrchat.cloud/api/1/avatars/" + avatarID + "?apiKey=" + apiKey);
                                request.Headers.Add(HttpRequestHeader.Authorization,$"BASIC {userAuth}");
                                request.Timeout = 2500;
                                try {
                                    string aviName = "", aviThumb = "";
                                    var response = (HttpWebResponse) request.GetResponse();
                                    using (StreamReader sr = new StreamReader(response.GetResponseStream())) {
                                        var data = sr.ReadToEnd();
                                        if (!data.Contains("name")) {
                                            log($"WARN: Could not fetch data for '{avatarID}'. Maybe it was removed?");
                                            continue;
                                        } else {
                                            log($"Data is valid...");
                                            var matches = Regex.Matches(data,"(\"name\":\"([^\"]+)\")");
                                            if (matches.Count != 0)
                                                aviName = matches[0].Value.Replace("\"","").Replace(":","").Replace("name","").Trim();
                                            if (data.Contains("thumbnailImageUrl")) {
                                                var thumbMatches = Regex.Matches(data,"(\"thumbnailImageUrl\":\"([^\"]+)\")");
                                                if (thumbMatches.Count != 0)
                                                    aviThumb = thumbMatches[0].Value.Replace("\"","").Replace(":","").Replace("thumbnailImageUrl","").Trim();
                                            }
                                        }
                                    }
                                    if (!string.IsNullOrWhiteSpace(aviName) && !string.IsNullOrWhiteSpace(aviThumb)) {
                                        avatarData += "  {\n" +
                                                    $"    \"ThumbnailImageUrl\": \"{aviThumb}\",\n" +
                                                    $"    \"AvatarID\": \"{avatarID}\",\n" +
                                                    $"    \"Name\": \"{aviName}\",\n" +
                                                    "  }, \n";
                                        ++sucessful;
                                    } else log($"Getting avatar data for '{avatarID}' failed. (Missing name or thumbnail url.)");
                                } catch (WebException ex) {
                                    log($"WARN: Encountered WebException while fetching '{avatarID}' data: \n{ex.Message}\n");
                                }
                            }
                            st.Stop();
                            toggleGui(false,true);
                            log($"Process complete! Fetched {sucessful} avatars in {(int)(st.ElapsedMilliseconds/1000)} seconds.");
                            return true;
                        }
                    }
                    else log("ERR: Avatar ID list is invalid, no ID's were found!");
                } else log("ERR: Missing API key! (Did you log in yet?)");
            } else log("ERR: You must log in to VRCAPI first!");
            return false;
        }

        private bool logInUser() {
            if (!string.IsNullOrWhiteSpace(userAuth)) {
                var request = (HttpWebRequest) WebRequest.Create("https://api.vrchat.cloud/api/1/auth/user");
                request.Headers.Add(HttpRequestHeader.Authorization, $"BASIC {userAuth}");
                request.Timeout = 2500;
                log("Logging in to VRCApi...");
                try {
                    if (((HttpWebResponse) request.GetResponse()).StatusCode == HttpStatusCode.OK) {
                        var response = (HttpWebResponse) request.GetResponse();
                        using (StreamReader sr = new StreamReader(response.GetResponseStream())) {
                            var data = sr.ReadToEnd();
                            if (data.Contains("username")) {
                                var matches = Regex.Matches(data,"(\"username\":\"[A-Za-z0-9-_ ]+\")");
                                if (matches.Count != 0) {
                                    var userReturned = matches[0].Value.Replace("\"","").Replace(":","").Replace("username","").Trim();
                                    log($"VRC ack login request for user '{userReturned}'");
                                    if (userReturned.Equals(usrnameInputText.Text)) return true;
                                    else log("Login attempt failed: Incorrect user!");
                                } else {
                                    log("Login attempt failed: Incorrect reply!");
                                    log($"VRChat replied: \n\n{data}");
                                }
                            }
                        }
                    } 
                } catch (WebException ex) {
                    if (ex.Message.Contains("401")) log($"ERR: Incorrect username or password! ({ex.Message})");
                }
            } 
            else log("ERR: User auth string was null!");
            return false;
        }

        private bool fetchApiKey() {
            var request = (HttpWebRequest) WebRequest.Create("https://api.vrchat.cloud/api/1/config");
            request.Timeout = 2500;
            log("Requesting remote config...");
            if (((HttpWebResponse) request.GetResponse()).StatusCode == HttpStatusCode.OK) {
                try {
                    var response = (HttpWebResponse) request.GetResponse();
                    using (StreamReader sr = new StreamReader(response.GetResponseStream())) {
                        var data = sr.ReadToEnd();
                        if (data.Contains("apiKey")) {
                            var matches = Regex.Matches(data,"(\"apiKey\":\"[A-Za-z0-9]+\")");
                            if (matches.Count != 0) apiKey = matches[0].Value.Replace("\"","").Replace(":","").Replace("apiKey","").Trim();
                            var success = !string.IsNullOrWhiteSpace(apiKey);
                            log(success ? $"Found apiKey: '{apiKey}'" : "ERR: Could not fetch Api key.");
                            return success;
                        }
                    }
                } catch (WebException) { }
            }
            return false;
        }

        private void clearJsonBtn_Click(object sender,RoutedEventArgs e) { log($"Clearing JSON Output"); avatarOutputText.Clear(); }

        private void exitBtn_Click(object sender,RoutedEventArgs e) => quit();

        internal void toggleGui(bool locked, bool allUI = false) {
            this.Dispatcher.BeginInvoke(new Action(() => {
                aviIdInputText.IsEnabled = !locked;
                fetchBtn.IsEnabled = !locked;
                avatarOutputText.IsEnabled = !locked;
                clearJsonBtn.IsEnabled = !locked;
                singleInput.IsEnabled = !locked;
                multiInput.IsEnabled = !locked;
                if (allUI) {
                    usrnameInputText.IsEnabled = !locked;
                    passwordInputText.IsEnabled = !locked;
                    loginBtn.IsEnabled = !locked;
                }
            }));
        }

        internal void log(string message) {
            consoleOutputText.AppendText(message + Environment.NewLine);
            consoleOutputText.ScrollToEnd();
        }

        internal void quit() => Environment.Exit(0);

        private void singleInput_Checked(object sender,RoutedEventArgs e) {
            if (multiInput != null && aviIdInputText != null) {
                shouldMultiInput = (multiInput.IsChecked == true);
                aviIdInputText.Height = shouldMultiInput ? 100 : 28;
                aviIdInputText.AcceptsReturn = shouldMultiInput;
                aviIdInputText.VerticalScrollBarVisibility = shouldMultiInput ? ScrollBarVisibility.Auto : ScrollBarVisibility.Disabled;
                aviIdInputText.TextWrapping = shouldMultiInput ? TextWrapping.Wrap : TextWrapping.NoWrap;
            }
        }
    }
}
