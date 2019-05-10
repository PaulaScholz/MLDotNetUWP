//***********************************************************************
//
// Copyright (c) 2019 Microsoft Corporation. All rights reserved.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
// THE SOFTWARE.
//
//**********************************************************************​

using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Collections.Generic;
using Windows.Foundation.Collections;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using System.Runtime.CompilerServices;
using Windows.ApplicationModel.AppService;
using Windows.UI.Core;
using Windows.Storage;
using System.Threading.Tasks;

namespace MLDotNetUWP
{
    /// <summary>
    /// We don't have a ViewModel in this simple example, rather, the page itself contains 
    /// the properties and does change notification.
    /// </summary>
    public partial class SamplePage : Page, INotifyPropertyChanged
    {
        // A static reference to this SamplePage instance so we can hook up the Connection handler.
        // Eliminates the need for dependency injection.  The handler is hooked up in App.xaml.cs, in
        // the OnBackgroundActivated handler, fired when the fullTrustProcess opens a connection to us.
        public static SamplePage Current;

        private float sepalWidthValue = 0;
        public float SepalWidthValue
        {
            get { return sepalWidthValue; }
            set {
                    Set(ref sepalWidthValue, value);
                    InvokeIrisPrediction.RaiseCanExecuteChanged();
                }
        }

        private float sepalLengthValue = 0;
        public float SepalLengthValue
        {
            get { return sepalLengthValue; }
            set {
                    Set(ref sepalLengthValue, value);
                    InvokeIrisPrediction.RaiseCanExecuteChanged();
                }
        }

        private float petalWidthValue = 0;
        public float PetalWidthValue
        {
            get { return petalWidthValue; }
            set {
                    Set(ref petalWidthValue, value);
                    InvokeIrisPrediction.RaiseCanExecuteChanged();
                }
        }

        private float petalLengthValue = 0;
        public float PetalLengthValue
        {
            get { return petalLengthValue; }
            set {
                    Set(ref petalLengthValue, value);
                    InvokeIrisPrediction.RaiseCanExecuteChanged();
                }
        }

        public RelayCommand InvokeIrisPrediction
        {
            get;
            private set;
        }

        private string irisContents;

        public SamplePage()
        {
            // set our static 
            Current = this;

            this.InitializeComponent();

            Loaded += SamplePage_Loaded;

            InvokeIrisPrediction = new RelayCommand(InvokePrediction, CanInvokePrediction);
        }

        public bool CanInvokePrediction()
        {
            return PetalLengthValue > 0 && PetalWidthValue > 0 && SepalLengthValue > 0 && SepalWidthValue > 0;
        }

        /// <summary>
        /// command the MLDotNetWin32 fullTrust application to make a prediction
        /// </summary>
        public async void InvokePrediction()
        {
            // no need to open a connection, if we got this far we have one
            ValueSet valueSet = new ValueSet();

            // program that receives this valueset will switch on the value of the verb
            valueSet.Add("verb", "makePrediction");
            valueSet.Add("sl", SepalLengthValue);
            valueSet.Add("sw", SepalWidthValue);
            valueSet.Add("pl", PetalLengthValue);
            valueSet.Add("pw", PetalWidthValue);

            AppServiceResponse response = null;

            try
            {
                // send the command and wait for a response
                response = await App.Connection.SendMessageAsync(valueSet);

                // if the command is a success, get the new results
                if (response?.Status == AppServiceResponseStatus.Success)
                {
                    string verb = (string)response.Message["verb"];

                    if("PredictionResult" == verb)
                    {
                        uint cluster = (uint)response.Message["Cluster"];
                        string distances = (string)response.Message["Distances"];

                        MainPage.Current?.NotifyUser(string.Format("Cluster: {0}, Distances: {1}", cluster,distances), NotifyType.StatusMessage);
                    }
                    else if ("PredictionError" == verb)
                    {
                        string exceptionMessage = (string)response.Message["exceptionMessage"];

                        MainPage.Current?.NotifyUser(string.Format("PredictionError Exception Message: {0}", exceptionMessage), NotifyType.ErrorMessage);
                    }
                    else if ("APIError" == verb)
                    {
                        string exceptionMessage = (string)response.Message["exceptionMessage"];

                        MainPage.Current?.NotifyUser(string.Format("APIError Exception Message: {0}", exceptionMessage), NotifyType.ErrorMessage);
                    }
                    else
                    {
                        MainPage.Current?.NotifyUser("Unknown Exception", NotifyType.ErrorMessage);
                    }
                }
            }
            catch (Exception ex)
            {
                MainPage.Current?.NotifyUser(string.Format("InvokePrediction Exception. Message {0}", ex.Message.ToString()), NotifyType.ErrorMessage);
            }
        }

        /// <summary>
        /// Launch the full trust MLDotNetWin32 process.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private async void SamplePage_Loaded(object sender, RoutedEventArgs e)
        {
            ReadIrisData();

            // set tutorial default values into textboxes
            SepalLengthValue = 5.1f;
            SepalWidthValue = 3.5f;
            PetalLengthValue = 1.4f;
            PetalWidthValue = 0.2f;

            // Launch the MLDotNetWin32 background process. When launched, it will build a Prediction model
            // for Iris flowers and send prediction results back to us through our Connection_RequestReceived event handler.
            await Windows.ApplicationModel.FullTrustProcessLauncher.LaunchFullTrustProcessForCurrentAppAsync();
        }

        /// <summary>
        /// Read the Iris data and send it to the win32 process so it can build a model
        /// </summary>
        /// <returns></returns>
        public async void ReadIrisData()
        {
            Uri irisUri = new Uri("ms-appx:///Assets/iris-data.txt");

            StorageFile irisFile = await StorageFile.GetFileFromApplicationUriAsync(irisUri);

            irisContents = await FileIO.ReadTextAsync(irisFile);
        }

        public async Task BuildModel()
        {
            // no need to open a connection, if we got this far we have one
            ValueSet valueSet = new ValueSet();
            valueSet.Add("verb", "buildModel");
            valueSet.Add("irisData", irisContents);

            AppServiceResponse response = null;

            try
            {
                // send the command and wait for a response
                response = await App.Connection.SendMessageAsync(valueSet);

                // if the command is a success, get the new results
                if (response?.Status == AppServiceResponseStatus.Success)
                {
                    string verb = (string)response.Message["verb"];

                    if ("modelOk" == verb)
                    {
                        MainPage.Current?.NotifyUser("Iris model built successfully.", NotifyType.StatusMessage);
                    }
                    else
                    {
                        MainPage.Current?.NotifyUser("Iris model build failure.", NotifyType.ErrorMessage);
                    }
                }
                else
                {
                    MainPage.Current?.NotifyUser(string.Format("ReadIrisData AppServiceResponse was {0}", response.Status.ToString()), NotifyType.ErrorMessage);
                }
            }
            catch (Exception ex)
            {
                MainPage.Current?.NotifyUser(string.Format("(Exception in ReadIrisData. Message {0}", ex.Message.ToString()), NotifyType.ErrorMessage);
            }
        }


        /// <summary>
        /// Called by App.xaml.cs OnBackgroundActivated through static Current ref
        /// </summary>
        public void RegisterConnection()
        {
            if (App.Connection != null)
            {
                App.Connection.RequestReceived += Connection_RequestReceived;
            }
        }

        /// <summary>
        /// This is not used in this demo, but is the pattern if you need it.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="args"></param>
        private async void Connection_RequestReceived(AppServiceConnection sender, AppServiceRequestReceivedEventArgs args)
        {
            var deferral = args.GetDeferral();

            ValueSet message = args.Request.Message;
            ValueSet returnData = new ValueSet();
            returnData.Add("response", "success");

            // get the verb or "command" for this request
            string verb = message["verb"] as String;

            switch (verb)
            {

            }

            try
            {
                // Return the data to the caller.
                await args.Request.SendResponseAsync(returnData);
            }
            catch (Exception e)
            {
                // Your exception handling code here.
            }
            finally
            {
                // Complete the deferral so that the platform knows that we're done responding to the app service call.
                // Note for error handling: this must be called even if SendResponseAsync() throws an exception.
                deferral.Complete();
            }
        }

        #region PropertyChange Notifications
        public event PropertyChangedEventHandler PropertyChanged;
        protected void RaisePropertyChanged(string name)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }

        /// <summary>
        /// Property setter for UI-bound values.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="storage"></param>
        /// <param name="value"></param>
        /// <param name="propertyName"></param>
        protected void Set<T>(ref T storage, T value, [CallerMemberName]string propertyName = null)
        {
            if (Equals(storage, value))
            {
                return;
            }

            storage = value;
            RaisePropertyChanged(propertyName);
        }
        #endregion

        private async void Button_Click(object sender, RoutedEventArgs e)
        {
            await BuildModel();
        }
    }
}
