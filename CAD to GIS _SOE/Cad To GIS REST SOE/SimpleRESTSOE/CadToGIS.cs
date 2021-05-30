// Copyright 2014 ESRI
// 
// All rights reserved under the copyright laws of the United States
// and applicable international laws, treaties, and conventions.
// 
// You may freely redistribute and use this sample code, with or
// without modification, provided you include the original copyright
// notice and use restrictions.
// 
// See the use restrictions at <your ArcGIS install location>/DeveloperKit10.3/userestrictions.txt.
// 

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using System.Collections.Specialized;

using System.Runtime.InteropServices;

using ESRI.ArcGIS.esriSystem;
using ESRI.ArcGIS.Server;
using ESRI.ArcGIS.Geometry;
using ESRI.ArcGIS.Geodatabase;
using ESRI.ArcGIS.Carto;
using ESRI.ArcGIS.SOESupport;
using System.IO;
using ESRI.ArcGIS.DataSourcesFile;

using ESRI.ArcGIS.AnalysisTools;
using ESRI.ArcGIS.Geoprocessor;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using ESRI.ArcGIS.DataSourcesGDB;

namespace NetSimpleRESTSOE
{
    [ComVisible(true)]
    [Guid("592d9b60-bb6f-49f9-9429-e9c720bca615")]
    [ClassInterface(ClassInterfaceType.None)]
    [ServerObjectExtension("MapServer",
        AllCapabilities = "",
        DefaultCapabilities = "",
        Description = "Convert Cad to GIS Data .Net",
        DisplayName = "Convert Cad to GIS Data SOE",
        Properties = "",
        SupportsREST = true,
        SupportsSOAP = false)]
    public class CadToGIS : IServerObjectExtension, IRESTRequestHandler
    {
        private string soeName;
        private IServerObjectHelper soHelper;
        private ServerLogger serverLog;
        private IRESTRequestHandler _reqHandler;
        private IMapServerDataAccess mapServerDataAccess;
        private IMapLayerInfos layerInfos;

        private string localFilePath = string.Empty;
        private string virtualFilePath = string.Empty;
        private string environmentUrl = @"F:\apps\CadFile\Data\Madinah_27-12-2020\shp";
        JObject o2jdonSplitLine;
        JObject o2jdonShp;

        public CadToGIS()
        {
            soeName = this.GetType().Name;
            _reqHandler = new SoeRestImpl(soeName, CreateRestSchema()) as IRESTRequestHandler;
        }

        public void Init(IServerObjectHelper pSOH)
        {
            this.soHelper = pSOH;
            //string _outputDirectory = "C:\\arcgisserver\\directories\\arcgisoutput"; 
            this.serverLog = new ServerLogger();
            this.mapServerDataAccess = (IMapServerDataAccess)this.soHelper.ServerObject;
            IMapServer3 ms = (IMapServer3)this.mapServerDataAccess;
            IMapServerInfo mapServerInfo = ms.GetServerInfo(ms.DefaultMapName);
            this.layerInfos = mapServerInfo.MapLayerInfos;

            serverLog.LogMessage(ServerLogger.msgType.infoStandard, this.soeName + ".init()", 200, "Initialized " + this.soeName + " SOE.");

            localFilePath = @"C:\arcgisserver\directories\arcgissystem\arcgisuploads\services\cadToGIsFinal.GPServer\";
            virtualFilePath = pSOH.ServerObject.ConfigurationName + "_" + pSOH.ServerObject.TypeName;
        }

        public void Shutdown()
        {
            serverLog.LogMessage(ServerLogger.msgType.infoStandard, this.soeName + ".init()", 200, "Shutting down " + this.soeName + " SOE.");
            this.soHelper = null;
            this.serverLog = null;
            this.mapServerDataAccess = null;
            this.layerInfos = null;
        }

        private RestResource CreateRestSchema()
        {
            RestResource soeResource = new RestResource(soeName, false, RootResHandler);

            RestOperation ConvertCadToGISOp = new RestOperation("ConvertCadToGIS",
                                                     new string[] { "inputFile" },
                                                      new string[] { "json" },
                                                      ConvertCadToGIS);

            soeResource.operations.Add(ConvertCadToGISOp);

            return soeResource;
        }

        public string GetSchema()
        {
            return _reqHandler.GetSchema();
        }
        byte[] IRESTRequestHandler.HandleRESTRequest(string Capabilities,
            string resourceName,
            string operationName,
            string operationInput,
            string outputFormat,
            string requestProperties,
            out string responseProperties)
        {
            return _reqHandler.HandleRESTRequest(Capabilities, resourceName, operationName, operationInput, outputFormat, requestProperties, out responseProperties);
        }

        private byte[] RootResHandler(System.Collections.Specialized.NameValueCollection boundVariables,
            string outputFormat,
            string requestProperties,
            out string responseProperties)
        {
            responseProperties = null;

            JSONObject json = new JSONObject();
            json.AddString("name", "Convert Cad to GIS Data ");
            json.AddString("description", "Convert Cad to GIS Data with arcobject for .Net");
            return Encoding.UTF8.GetBytes(json.ToJSONString(null));
        }

        private byte[] ConvertCadToGIS(NameValueCollection boundVariables,
                                                  JsonObject operationInput,
                                                      string outputFormat,
                                                      string requestProperties,
                                                  out string responseProperties)
        {
            responseProperties = "";
            

            //System.Diagnostics.Debugger.Launch();   //for dubuging

            string inputFile = string.Empty;

            bool found = operationInput.TryGetString("inputFile", out inputFile);

            string file = localFilePath + inputFile + "\\" + "parcle.dwg";

            long fileSize = new System.IO.FileInfo(file).Length;
           
            string message = "";

            // Initialize the Geoprocessor
            Geoprocessor GP = new Geoprocessor();

            // Set workspace environment
            GP.SetEnvironmentValue("workspace", environmentUrl);
            // Initialize the Conversion Tool
            ESRI.ArcGIS.ConversionTools.FeatureClassToShapefile FShape = new ESRI.ArcGIS.ConversionTools.FeatureClassToShapefile();

            FShape.Input_Features = file + "\\Polygon";
            Guid g = Guid.NewGuid();
            if (!Directory.Exists(environmentUrl+ "\\" + g ))
            {
                Directory.CreateDirectory(environmentUrl + "\\" + g);
            }
            FShape.Output_Folder = environmentUrl + "\\" + g;

            try
            {
                GP.Execute(FShape, null);

                ESRI.ArcGIS.DataManagementTools.AddGeometryAttributes aGeoAttr = new ESRI.ArcGIS.DataManagementTools.AddGeometryAttributes();
                aGeoAttr.Input_Features = environmentUrl + "\\" + g + "\\parcle_dwg_Polygon.shp";
                aGeoAttr.Geometry_Properties = "AREA";
                try
                {
                    GP.Execute(aGeoAttr, null);
                }
                catch (Exception ex)
                {
                    message = ex.ToString();
                }


                ESRI.ArcGIS.ConversionTools.FeaturesToJSON FShpToJson = new ESRI.ArcGIS.ConversionTools.FeaturesToJSON();

                FShpToJson.in_features = environmentUrl + "\\" + g + "\\parcle_dwg_Polygon.shp";

                //FJson.out_json_file = @"F:\apps\CadFile\Data\Madinah_27-12-2020\myjson.json";
                Guid gg = Guid.NewGuid();
                FShpToJson.out_json_file = environmentUrl + "\\" + gg + "Shapefile" + ".json";
                string jdonShp = environmentUrl + "\\" + gg + "Shapefile" + ".json";

                FShpToJson.format_json = "FORMATTED";
                try
                {
                    GP.Execute(FShpToJson, null);

                    //read file
                    JObject o1 = JObject.Parse(File.ReadAllText(jdonShp));

                    // read JSON directly from a file
                    using (StreamReader jsonFile = File.OpenText(jdonShp))
                    using (JsonTextReader reader = new JsonTextReader(jsonFile))
                    {
                        o2jdonShp = (JObject)JToken.ReadFrom(reader);
                    }
                }
                catch (Exception ex)
                {
                    message = ex.ToString();
                }
            }
            catch (Exception ex)
            {
                message = ex.ToString();
            }

            ESRI.ArcGIS.DataManagementTools.FeatureToLine FToLine = new ESRI.ArcGIS.DataManagementTools.FeatureToLine();

            FToLine.in_features = environmentUrl + "\\" + g + "\\parcle_dwg_Polygon.shp";
            Guid gToline = Guid.NewGuid();

            string FToLineOutput = @"F:\apps\CadFile\Data\Madinah_27-12-2020\testDB\testDB.gdb" ;
            string FToLineOutputing = FToLineOutput + "\\parcle_dwg_Polygon_FeatureTo" + gToline.ToString("N").Substring(0, 7);
            FToLine.out_feature_class = FToLineOutputing;
            try
            {
                GP.Execute(FToLine, null);
            }
            catch (Exception ex)
            {
                message = ex.ToString();
            }

            Guid gSplitLine = Guid.NewGuid();

            ESRI.ArcGIS.DataManagementTools.SplitLine SLP = new ESRI.ArcGIS.DataManagementTools.SplitLine();
          
            SLP.in_features = FToLineOutputing;

            string out_feature_class = FToLineOutput + "\\parcle_dwg_Polygon_FeatureTo2" + gSplitLine.ToString("N").Substring(0, 7); 
           
            SLP.out_feature_class = out_feature_class;


            try
            {
                GP.Execute(SLP, null);

                ESRI.ArcGIS.ConversionTools.FeaturesToJSON FSLineToJson = new ESRI.ArcGIS.ConversionTools.FeaturesToJSON();

                FSLineToJson.in_features = out_feature_class;

                //FJson.out_json_file = @"F:\apps\CadFile\Data\Madinah_27-12-2020\myjson.json";
                Guid SLgg = Guid.NewGuid();

                FSLineToJson.out_json_file = environmentUrl + "\\" + SLgg + "SplitLine" + ".json";
                string jdonSplitLine = environmentUrl + "\\" + SLgg + "SplitLine" + ".json";

                FSLineToJson.format_json = "FORMATTED";
                try
                {
                    GP.Execute(FSLineToJson, null);
                    //read file
                    JObject o1 = JObject.Parse(File.ReadAllText(jdonSplitLine));

                    // read JSON directly from a file
                    using (StreamReader jsonFile = File.OpenText(jdonSplitLine))
                    using (JsonTextReader reader = new JsonTextReader(jsonFile))
                    {
                        o2jdonSplitLine = (JObject)JToken.ReadFrom(reader);
                    }
                }
                catch (Exception ex)
                {
                    message = ex.ToString();
                }
            }
            catch (Exception ex)
            {
                message = ex.ToString();
            }

            if (outputFormat == "json")
            {
                responseProperties = "{\"Content-Type\" : \"application/json\"}";

                JsonObject jsonResult = new JsonObject();
                if (!String.IsNullOrEmpty(message))
                {
                    jsonResult.AddString("errors", message.ToString());
                }
                jsonResult.AddString("Lines", o2jdonSplitLine.ToString());
                jsonResult.AddString("Polygon", o2jdonShp.ToString());
                //jsonResult.AddString("fileName", inputFile);
                //jsonResult.AddString("fileSizeBytes", Convert.ToString(fileSize));
                return Encoding.UTF8.GetBytes(jsonResult.ToJson());

            }
            else if (outputFormat == "file")
            {
                responseProperties = "{\"Content-Type\" : \"application/octet-stream\",\"Content-Disposition\": \"attachment; filename=" + inputFile + "\"}";
                return System.IO.File.ReadAllBytes(file);
            }
            return Encoding.UTF8.GetBytes("");
        }

        private byte[] createErrorObject(int codeNumber, String errorMessageSummary, String[] errorMessageDetails)
        {
            if (errorMessageSummary.Length == 0 || errorMessageSummary == null)
            {
                throw new Exception("Invalid error message specified.");
            }

            JSONObject errorJSON = new JSONObject();
            errorJSON.AddLong("code", codeNumber);
            errorJSON.AddString("message", errorMessageSummary);

            if (errorMessageDetails == null)
            {
                errorJSON.AddString("details", "No error details specified.");
            }
            else
            {
                String errorMessages = "";
                for (int i = 0; i < errorMessageDetails.Length; i++)
                {
                    errorMessages = errorMessages + errorMessageDetails[i] + "\n";
                }

                errorJSON.AddString("details", errorMessages);
            }

            JSONObject error = new JSONObject();
            errorJSON.AddJSONObject("error", errorJSON);

            return Encoding.UTF8.GetBytes(errorJSON.ToJSONString(null));
        }

    }
}