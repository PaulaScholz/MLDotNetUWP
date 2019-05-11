# MLDotNetUWP
## Windows Developer Incubation and Learning - Paula Scholz

In March, 2019, it was mentioned in an ML.Net presentation by Microsoft Principal Program Manager Cesar De la Torre Llorente that the preliminary ML.Net SDK, version 0.11, did not yet support the Universal Windows Platform (UWP) because of UWP limitations on reflection.  I decided to test this proposition by implementing the [.Net Iris console tutorial](https://docs.microsoft.com/en-us/dotnet/machine-learning/tutorials/iris-clustering "Tutorial: Categorize iris flowers using a clustering model with ML.NET"), which uses the famous [Anderson Iris Flower Data Set](https://en.wikipedia.org/wiki/Iris_flower_data_set "Wikipedia Iris Flower Data Set article"), in a [UWP Desktop Bridge](https://docs.microsoft.com/en-us/windows/uwp/porting/desktop-to-uwp-extend "Extend your desktop application with modern UWP components") pure Win32 "fullTrustProcess" and only rely on UWP for the user interface.

Microsoft has provided an official ML.Net 1.0 [Iris Classification](https://github.com/dotnet/machinelearning-samples/tree/master/samples/csharp/getting-started/MulticlassClassification_Iris "Multiclass Iris Classification") console application sample that provides more information on this topic.

When originally built using ML.Net version 0.11, this UWP sample did run but always provided blank predictions, regardless of the input used.  However, when the sample was upgraded to the current release version, ML.Net 1.0, and the `MLDotNetWin32` code updated to the current API, the application did start to provide predictions.  There are several problems however, to wit;

* There are EETypeLoadExceptions when loading code that uses ML.Net in a UWP environment, both with the 0.11 version and the released 1.0 version.
* Predictions are only consistent with the ML.Net tutorial results some of the time, but it is perfectly normal in machine learning that different trainings using the same algorithm and data might sometimes get different results.
* The ML.Net 1.0 version will not run outside the debugger environment.  When the sample is run from the Start menu, it immediately terminates when loading the Win32 "RunFullTrust" ML.Net component. ML.Net does not yet officially support UWP, so issues are to be expected.

These problems aside, this sample does run under Visual Studio 2017 and 2019 in Debug mode and can provide insights into development of ML.Net applications under UWP when that environment becomes officially supported by Microsoft.

The packaging architecture is shown below:

![MLDotNetUWP Package Applications](/docimages/MLDotNetUWP_PackageApplications.png "MLDotNetUWP Package Applications")

The resulting application looks like this:

![MLDotNetUWP Application User Interface](/docimages/MLDotNetUWP.png "MLDotNetUWP User Interface")

The initial IrisData values from the ML.Net tutorial are provided as default values in the UWP input textbox objects.

```csharp
internal static readonly IrisData Setosa = new IrisData
{
    SepalLength = 5.1f,
    SepalWidth = 3.5f,
    PetalLength = 1.4f,
    PetalWidth = 0.2f
}
```

The iris flower data is passed from the UWP application via an `AppServiceConnection` because the ML.Net file system functions will not operate in the context of a UWP appx package.  So, we read the iris data via UWP Storage functions in the UWP app, pass this data to the Win32 app via the `AppServiceConnection` and inside the `MLDotNetWin32` program we create a `List<IrisData>` to act as an `IEnumerable` to create our ML.Net `IDataView` object.  `ReadIrisData()` is called from the `SamplePage.Loaded()` event handler, and the code that calls `MLDotNetWin32` is in the `BuildModel()` button event handler.

```csharp
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

                        // enable the Predict button if we also have Petal and Sepal values
                        ModelHasBeenBuilt = true;
                    }
                    else
                    {
                        string exceptionMessage = (string)response.Message["exceptionMessage"];
                        MainPage.Current?.NotifyUser(string.Format("Iris model build failure. Message {0}", exceptionMessage), NotifyType.ErrorMessage);
                    }
                }
                else
                {
                    MainPage.Current?.NotifyUser(string.Format("ReadIrisData AppServiceResponse was {0}", response.Status.ToString()), NotifyType.ErrorMessage);
                }
            }
            catch (Exception ex)
            {
                MainPage.Current?.NotifyUser(string.Format("(Exception in BuildModel. Message {0}", ex.Message.ToString()), NotifyType.ErrorMessage);
            }
        }
```

And, in `MLDotNetWin32`, inside the `Connection_RequestReceived` handler that accepts `AppServiceConnection` requests, we process the Iris data like this:

```csharp
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
                            //
                            // This code is from the old tutorial in ML.net version 0.11
                            //var pipeline = mlContext.Transforms.Conversion.MapValueToKey("Label")                            
                            //    .Append(mlContext.Transforms.Concatenate("Features", "SepalLength", "SepalWidth", "PetalLength", "PetalWidth"))
                            //    .AppendCacheCheckpoint(mlContext)
                            //    .Append(mlContext.MulticlassClassification.Trainers.StochasticDualCoordinateAscent(labelColumnName: "Label", featureColumnName: "Features"))
                            //    .Append(mlContext.Transforms.Conversion.MapKeyToValue("PredictedLabel"));

                            // this is the new ML.Net 1.0 way of doing things.
                            string featuresColumnName = "Label";
                            var pipeline = mlContext.Transforms
                                .Concatenate(featuresColumnName, "SepalLength", "SepalWidth", "PetalLength", "PetalWidth")
                                .Append(mlContext.Clustering.Trainers.KMeans(featuresColumnName, numberOfClusters: 3));

                            // Train your model based on the data set
                            model = pipeline.Fit(trainingDataView);

                            // let UWP know our model built correctly
                            returnData.Add("verb", "modelOk");
                        }
                        catch (Exception ex)
                        {
                            returnData.Add("verb", "modelFailure");
                        }

                        break;
                    }
```

Note that the older 0.11 version calls remain in the code for contrast, but are commented out.

Once the model has been built, simply enter Sepal and Petal values into the text boxes and press the Predict button.  The UWP button handler code looks like this:

```csharp
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
```

The corresponding code in the `MLDotNetWin32` app service looks like this:

```csharp
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
```

Note the commented-out ML.Net 0.11 version prediction code.

Each time the model is built and run, different prediction results may be returned for the same data.  The procedure used was to press the `Build Model` button and then press the `Predict` button, using the same Sepal and Petal values.  Here are some examples, only one of which is in Cluster 2 with the correct distances, although in opposite order of the example given in the ML.Net tutorial, but this can be ignored:

![MLDotNetUWP Cluster 2 output after model rebuild](/docimages/MLDotNetUWP_Cluster2Output_afterModelRebuild.PNG "Cluster 2 output after model rebuild, correct result")

Sometimes you will get a different Cluster result after rebuilding the model, with the same Petal and Sepal input:

![MLDotNetUWP Cluster 1 output after model rebuild](/docimages/MLNetUWPDemo_screenshot_wrongAnswer.PNG "Cluster 1 output after model rebuild, wrong result")

**Note that while the Cluster predictions are different in each example, the Distance values are almost the same, but in different order.**

An example of the EETypeLoadException in the Debug output is shown below:
![EETypeLoadException](/docimages/MLDotNetUWP_debugOutput_EETypeLoadException.PNG)

A text file of the debug output is provided in the `docimages` folder accompanying the sample.

ML.Net is a promising addition to the Microsoft family of machine learning solutions and when it does eventually support the Universal Windows Platform, this sample may provide a point of departure for your own applications.

Paula Scholz,
Windows Developer Incubation and Learning,
May 10, 2019


