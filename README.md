# MLDotNetUWP
## Windows Developer Incubation and Learning - Paula Scholz

In March, 2019, it was mentioned in an ML.Net presentation by Microsoft Principal Program Manager Cesar De la Torre Llorente that the preliminary ML.Net SDK, version 0.11, did not yet support the Universal Windows Platform (UWP) because of UWP limitations on reflection.  I decided to test this proposition by implementing the [.Net Iris console tutorial](https://docs.microsoft.com/en-us/dotnet/machine-learning/tutorials/iris-clustering "Tutorial: Categorize iris flowers using a clustering model with ML.NET"), which uses the famous [Anderson Iris Flower Data Set](https://en.wikipedia.org/wiki/Iris_flower_data_set "Wikipedia Iris Flower Data Set article"), in a [UWP Desktop Bridge](https://docs.microsoft.com/en-us/windows/uwp/porting/desktop-to-uwp-extend "Extend your desktop application with modern UWP components") pure Win32 "fullTrustProcess" and only rely on UWP for the user interface.

When originally built using ML.Net version 0.11, the sample did run but always provided blank predictions, regardless of the input used.  However, when the sample was upgraded to the current release version, ML.Net 1.0, and the `MLDotNetWin32` code updated to the current API, the application did start to provide predictions.  There are several problems however, to wit;

* There are EETypeLoadExceptions when loading code that uses ML.Net, both with the 0.11 version and the released 1.0 version.
* Predictions are only consistent with the ML.Net tutorial results some of the time.  Rebuilding and running the model with the same data results in new and different predictions, which is unexpected and counterintuitive.
* The ML.Net 1.0 version will not run outside the debugger environment.  When run from the Start menu, it immediately terminates when loading the Win32 "RunFullTrust" ML.Net component.

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

The iris flower data is passed from the UWP application via an `AppServiceConnection` because the ML.Net file system functions will not operate in the context of a UWP appx package.  So, we read the iris data via UWP Storage functions, pass this data via the `AppServiceConnection` and inside the `MLDotNetWin32` program we create a `List<IrisData>` to act as an `IEnumerable` to create our ML.Net `IDataView` object.  That code looks like this:

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

                            // add the prediction to our response
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

Each time the model is built and run, different prediction results are returned for the same data.  The procedure used was to press the `Build Model` button and then press the `Predict` button, using the same Sepal and Petal values.  Here are some examples, only one of which is in Cluster 2 with the correct distances, although in opposite order of the example given in the ML.Net tutorial, but this can be ignored:

![MLDotNetUWP Cluster 2 output after model rebuild](/docimages/MLDotNetUWP_Cluster2Output_afterModelRebuild.PNG "Cluster 2 output after model rebuild, correct result")

The next example shows a different result, with the same input:

![MLDotNetUWP Cluster 1 output after model rebuild](/docimages/MLNetUWPDemo_screenshot_wrongAnswer.PNG "Cluster 1 output after model rebuild, wrong result")

And again, with a Cluster 3 result:
![MLDotNetUWP Cluster 3 output after model rebuild](/docimages/MLDotNetUWP_Cluster3Output_afterModelRebuild.PNG "Cluster 3 output after model rebuild")

Different results given the same input are not expected.  **Note that while the Cluster predictions are different in each example, the Distance values are almost the same, but in different order.**

An example of the EETypeLoadException in the Debug output is shown below:
![EETypeLoadException](/docimages/MLDotNetUWP_debugOutput_EETypeLoadException.PNG)

A text file of the debug output is provided in the `docimages` folder accompanying the sample.

My rudimentary knowledge of ML.Net precludes me from knowing what the issue might be, but the different results from the same input goes against my software development intuition.  Feel free to clone the project and discover the results for yourself.

Paula Scholz,
Windows Developer Incubation and Learning,
May 10, 2019


