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

using Microsoft.Data.DataView;
using Microsoft.ML;
using Microsoft.ML.Data;
using System;
using System.Diagnostics;
using System.Threading.Tasks;
using Windows.ApplicationModel.AppService;
using Windows.Foundation.Collections;


using System.IO;
using System.Linq;
using System.Collections.Generic;

// CS0649 compiler warning is disabled because some fields are only
// assigned to dynamically by ML.NET at runtime
#pragma warning disable CS0649

namespace MLDotNetWin32
{
    class Program
    {
        // STEP 1: Define your data structures
        // IrisData is used to provide training data, and as
        // input for prediction operations
        // - First 4 properties are inputs/features used to predict the label
        // - Label is what you are predicting, and is only set when training
        public class IrisData
        {
            [LoadColumn(0)]
            public float SepalLength;

            [LoadColumn(1)]
            public float SepalWidth;

            [LoadColumn(2)]
            public float PetalLength;

            [LoadColumn(3)]
            public float PetalWidth;

            [LoadColumn(4)]
            public string Label;
        }

        // IrisPrediction is the result returned from prediction operations
        //public class IrisPrediction
        //{
        //    //[ColumnName("PredictedLabel")]
        //    public string PredictedLabels;
        //}

        public class ClusterPrediction
        {
            [ColumnName("PredictedLabel")]
            public uint PredictedClusterId;

            [ColumnName("Score")]
            public float[] Distances;
        }

        // the AppServiceConnection to our UWP app
        private static AppServiceConnection connection = new AppServiceConnection();

        // HRESULT 80004005 is E_FAIL
        const int E_FAIL = unchecked((int)0x80004005);

        // Create a ML.NET environment
        private static MLContext mlContext = new MLContext();

        // create a ML.NET model
        //private static TransformerChain<Microsoft.ML.Transforms.KeyToValueMappingTransformer> model;
        private static TransformerChain<ClusteringPredictionTransformer<Microsoft.ML.Trainers.KMeansModelParameters>> model;

        private static List<IrisData> irisDataList = new List<IrisData>();

        static void Main(string[] args)
        {

            // The AppServiceName must match the name declared in the Packaging project's Package.appxmanifest file.
            // You'll have to view it as code to see the XML.  It will look like this:
            //
            //       <Extensions>
            //           <uap:Extension Category="windows.appService">
            //              <uap:AppService Name="CommunicationService" />
            //          </uap:Extension>
            //          <desktop:Extension Category="windows.fullTrustProcess" Executable="MLDotNetWin32\MLDotNetWin32.exe" />
            //       </Extensions>
            //
            // To debug this app, you'll need to have it started in console mode.  Uncomment 
            // the lines below and then right-click on the project file to get to project settings.
            // Select the Application tab and change the Output Type from Windows Application to 
            // Console Application.  A "Windows Application" is simply a headless console app.

            Console.WriteLine("Detatch your debugger from the UWP app and attach it to MLDotNetWin32.");
            Console.WriteLine("Set your breakpoint in MLDotNetWin32 and then press Enter to continue.");
            Console.ReadLine();

            Console.WriteLine("Got to here");

            connection.AppServiceName = "CommunicationService";
            connection.PackageFamilyName = Windows.ApplicationModel.Package.Current.Id.FamilyName;

            // hook up the connection event handlers
            connection.ServiceClosed += Connection_ServiceClosed;
            connection.RequestReceived += Connection_RequestReceived;

            AppServiceConnectionStatus result = AppServiceConnectionStatus.Unknown;

            // static void Main cannot be async until C# 7.1, so put this on the thread pool
            Task.Run(async () =>
            {
                // open a connection to the UWP host
                result = await connection.OpenAsync();

            }).GetAwaiter().GetResult();

            if (result == AppServiceConnectionStatus.Success)
            {
                // Let the app service connection handlers respond to events.  If this Win32 app had a Window,
                // this would be a message loop.  The app ends when the app service connection to 
                // the UWP app is closed and our Connection_ServiceClosed event handler is fired.
                while (true)
                {
                    // the below is necessary if this were calling COM and this was STAThread
                    // pump the underlying STA thread
                    // https://blogs.msdn.microsoft.com/cbrumme/2004/02/02/apartments-and-pumping-in-the-clr/
                    // Thread.CurrentThread.Join(0);
                }
            }
        }

        /// <summary>
        /// The UWP host has sent a request for something. Responses to the UWP app are set by
        /// the respective case handlers, and sent to the UWP Connection_RequestReceived handler
        /// via the AppServiceConnection.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="args"></param>
        private async static void Connection_RequestReceived(AppServiceConnection sender, AppServiceRequestReceivedEventArgs args)
        {
            var deferral = args.GetDeferral();

            ValueSet message = args.Request.Message;
            ValueSet returnData = new ValueSet();

            string verb = string.Empty;

            // get the command verb from the request message
            try
            {
                verb = message["verb"] as String;
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex.Message);

                verb = string.Empty;
            }

            switch (verb)
            {
                // we received a request to make a prediction
                case "makePrediction":
                    {
                        try
                        {
                            // get the parameters for the prediction from the message data
                            float sl = (float)message["sl"];
                            float sw = (float)message["sw"];
                            float pl = (float)message["pl"];
                            float pw = (float)message["pw"];

                            // we switch on the value of the verb in the UWP app that receives this valueSet
                            returnData.Add("verb", "PredictionResult");

                            // Use your model to make a prediction
                            // You can change these numbers to test different predictions
                            //var prediction = model.CreatePredictionEngine<IrisData, IrisPrediction>(mlContext).Predict(
                            //    new IrisData()
                            //    {
                            //        SepalLength = sl,
                            //        SepalWidth = sw,
                            //        PetalLength = pl,
                            //        PetalWidth = pw,
                            //    });

                            IrisData inputToTest = new IrisData()
                            {
                                SepalLength = sl,
                                SepalWidth = sw,
                                PetalLength = pl,
                                PetalWidth = pw,
                            };

                            var predictor = mlContext.Model.CreatePredictionEngine<IrisData, ClusterPrediction>(model);

                            var prediction = predictor.Predict(inputToTest);

                            // add the prediction to our response
                            returnData.Add("Cluster", prediction.PredictedClusterId);
                            returnData.Add("Distances", string.Join(" ", prediction.Distances));
                        }
                        catch (Exception ex)
                        {
                            returnData.Add("verb", "PredictionError");
                            returnData.Add("exceptionMessage", ex.Message.ToString());
                        }

                        break;
                    }

                // we received a request to build the model
                case "buildModel":
                    {
                        try
                        {
                            // get the Iris data from the message
                            string contents = message["irisData"] as string;

                            // build the List<IrisData>, which is an IEnumerable

                            var records = contents.Split(new[] { Environment.NewLine }, StringSplitOptions.None);

                            foreach(var record in records)
                            {
                                var fields = record.Split(',');

                                IrisData entry = new IrisData();
                                entry.SepalLength = float.Parse(fields[0]);
                                entry.SepalWidth = float.Parse(fields[1]);
                                entry.PetalLength = float.Parse(fields[2]);
                                entry.PetalWidth = float.Parse(fields[3]);
                                entry.Label = fields[4];

                                irisDataList.Add(entry);
                            }

                            // If working in Visual Studio, make sure the 'Copy to Output Directory'
                            // property of iris-data.txt is set to 'Copy always'
                            //IDataView trainingDataView = mlContext.Data.LoadFromTextFile<IrisData>(path: @"MLDotNetWin32\iris-data.txt", hasHeader: false, separatorChar: ',');


                            Microsoft.ML.IDataView trainingDataView = mlContext.Data.LoadFromEnumerable<IrisData>(irisDataList);

                            // Transform your data and add a learner
                            // Assign numeric values to text in the "Label" column, because only
                            // numbers can be processed during model training.
                            // Add a learning algorithm to the pipeline. e.g.(What type of iris is this?)
                            // Convert the Label back into original text (after converting to number in step 3)
                            //var pipeline = mlContext.Transforms.Conversion.MapValueToKey("Label")                            
                            //    .Append(mlContext.Transforms.Concatenate("Features", "SepalLength", "SepalWidth", "PetalLength", "PetalWidth"))
                            //    .AppendCacheCheckpoint(mlContext)
                            //    .Append(mlContext.MulticlassClassification.Trainers.StochasticDualCoordinateAscent(labelColumnName: "Label", featureColumnName: "Features"))
                            //    .Append(mlContext.Transforms.Conversion.MapKeyToValue("PredictedLabel"));

                            string featuresColumnName = "Label";
                            var pipeline = mlContext.Transforms
                                .Concatenate(featuresColumnName, "SepalLength", "SepalWidth", "PetalLength", "PetalWidth")
                                .Append(mlContext.Clustering.Trainers.KMeans(featuresColumnName, numberOfClusters: 3));

                            // Train your model based on the data set
                            //model = pipeline.Fit(trainingDataView);
                            model = pipeline.Fit(trainingDataView);

                            // add the prediction to our response
                            returnData.Add("verb", "modelOk");
                        }
                        catch (Exception ex)
                        {
                            returnData.Add("verb", "modelFailure");
                        }

                        break;
                    }

                default:
                    {
                        returnData.Add("verb", "APIError");
                        returnData.Add("exceptionMessage", "Bad or No Verb");
                        break;
                    }
            }

            try
            {
                // Return the data to the caller.
                await args.Request.SendResponseAsync(returnData);
            }
            catch (Exception e)
            {
                // Your exception handling code here.
                Debug.WriteLine(e.Message);
            }
            finally
            {
                // Complete the deferral so that the platform knows that we're done responding to the app service call.
                // Note for error handling: this must be called even if SendResponseAsync() throws an exception.
                deferral.Complete();
            }
        }

        /// <summary>
        /// Our UWP app service is closing, so shut ourselves down.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="args"></param>
        private static void Connection_ServiceClosed(AppServiceConnection sender, AppServiceClosedEventArgs args)
        {
            System.Environment.Exit(0);
        }
    }
}
