using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using Reclamation.Core;
using Reclamation.TimeSeries;

namespace RisehydrometGenerator
{
    class Program
    {
        public static DateTime t1, t2;

        static void Main(string[] args)
        {
            Reclamation.Core.Logger.InitTracing();
            Reclamation.Core.Logger._traceSwitch.Level = System.Diagnostics.TraceLevel.Off;

            // Read the control file
            string[] lines = File.ReadAllLines(@"riseHydrometItems.csv");

            // Create folder if !exists and delete file if it exists
            string folderPath = AppDomain.CurrentDomain.BaseDirectory + "jsonOutputs";
            string fileName = folderPath + @"\cpnRiseDataTransfer.json";
            if (!System.IO.Directory.Exists(folderPath))
            {
                System.IO.Directory.CreateDirectory(folderPath);
            }
            if (File.Exists(fileName))
            {
                File.Delete(fileName);
            }

            // Build output file
            using (StreamWriter sw = new StreamWriter(fileName))
            {
                // Initialize JSON container
                sw.Write("[");

                // Process control file entries
                bool firstEntry = true;
                foreach (var line in lines)
                {
                    // Only process non-comment lines
                    if (line.Length > 0 && line[0] != '#')
                    {
                        // Git items from input-line
                        string[] items = line.Split(',');
                        if (items.Count() >= 5)
                        {
                            // Define processing inputs
                            string cbtt = items[0].ToString();
                            string pcode = items[1].ToString();
                            string rtype = items[2].ToString();
                            string units = items[3].ToString();
                            string tstep = items[4].ToString();
                            Console.Write("Processing " + tstep + " " + cbtt + " " + pcode + "... ");
                            try
                            {
                                // only write comma separators for JSON arrays after the first one
                                if (!firstEntry)
                                {
                                    sw.Write(",");
                                }
                                else
                                {
                                    firstEntry = false;
                                }
                                // Define Hydromet series
                                // Resolve data query dates and time-step. Dates default to last 72 hours of instant data, 
                                // last 12 months of monthly data, or last 7 days of daily data if not passed into the program as an argument
                                var s = new Series();
                                switch (tstep)
                                {
                                    case "instant":
                                        s = new Reclamation.TimeSeries.Hydromet.HydrometInstantSeries(cbtt, pcode);
                                        t1 = DateTime.Now.Date.AddHours(-72);
                                        t2 = DateTime.Now.AddHours(-1);
                                        break;
                                    case "monthly":
                                        s = new Reclamation.TimeSeries.Hydromet.HydrometMonthlySeries(cbtt, pcode);
                                        t1 = DateTime.Now.Date.AddMonths(-12);
                                        t2 = DateTime.Now.Date.AddMonths(-1);
                                        break;
                                    default: //daily
                                        s = new Reclamation.TimeSeries.Hydromet.HydrometDailySeries(cbtt, pcode);
                                        t1 = DateTime.Now.Date.AddDays(-7);
                                        t2 = DateTime.Now.Date.AddDays(-1);
                                        break;
                                }
                                // Read data query dates if passed into program as arguments
                                if (args.Length == 2)
                                {
                                    t1 = DateTime.Parse(args[0]);
                                    t2 = DateTime.Parse(args[1]);
                                }
                                // Read Hydromet data
                                s.Read(t1, t2);
                                // Write JSON payload                            
                                sw.Write(ConvertToRiseJSON(s, cbtt, pcode, rtype, units, tstep));
                                Console.WriteLine("OK!");
                            }
                            catch
                            {
                                Console.WriteLine("FAIL!");
                            }
                        }
                    }
                }
                // Close JSON container
                sw.Write("]");
            }
        }

        /*
            * RISE JSON PAYLOAD FORMAT
            [
	            {
	            	"sourceCode": "cpnhydromet",
	            	"locationSourceCode": "gcl",
	            	"parameterSourceCode": "af",
	            	"dateTime": "1973-12-31 07:00:00",
	            	"result": 0,
	            	"status": null,
	            	"lastUpdate": "2021-02-12 10:00:00-07:00",
	            	"resultAttributes": {
	            		"resultType": "observed",
	            		"Units": "acre-feet"
	            	},
	            	"modelRunName": null,
	            	"modelRunDateTime": null,
	            	"modelRunDescription": null,
	            	"modelRunAttributes": null,
	            	"modelRunMemberDesc": null,
	            	"modelNameSourceCode": null,
	            	"modelRunSourceCode": null,
	            	"modelRunMemberSourceCode": null
	            },
                ...
            ]        
        */
        public static string jsonEntry = @"{""sourceCode"": ""cpnhydromet"",""locationSourceCode"": ""$CBTT$"",""parameterSourceCode"": ""$PCODE$"",""dateTime"": ""$ITH-T$"","
                + @"""result"": $ITH-VAL$,""status"": null,""lastUpdate"": ""$TNOW$"",""resultAttributes"": {""resultType"": ""$RTYPE$"",""Units"": ""$UNITS$""},"
                + @"""modelRunName"": null,""modelRunDateTime"": null,""modelRunDescription"": null,""modelRunAttributes"": null,""modelRunMemberDesc"": null,"
                + @"""modelNameSourceCode"": null,""modelRunSourceCode"": null,""modelRunMemberSourceCode"": null}";


        static string ConvertToRiseJSON(Series s, string cbtt, string pcode, string rType, string units, string tstep)
        {
            string jsonArray = "";
            // Loop through all the data points
            foreach (var point in s)
            {
                // Build JSON entry
                var ithT = point.DateTime;
                var ithVal = point.Value;
                var ithJsonEntry = jsonEntry.Replace("$CBTT$", cbtt);
                ithJsonEntry = ithJsonEntry.Replace("$PCODE$", pcode);
                ithJsonEntry = ithJsonEntry.Replace("$RTYPE$", rType);
                ithJsonEntry = ithJsonEntry.Replace("$UNITS$", units);
                ithJsonEntry = ithJsonEntry.Replace("$TNOW$", DateTime.Now.ToString("u"));
                ithJsonEntry = ithJsonEntry.Replace("$ITH-T$", ithT.ToString("u"));
                if (double.IsNaN(ithVal))
                {
                    ithJsonEntry = ithJsonEntry.Replace("$ITH-VAL$", "null");
                }
                else
                {
                    ithJsonEntry = ithJsonEntry.Replace("$ITH-VAL$", ithVal.ToString());
                }
                ithJsonEntry += ",";
                // Append JSON Entry
                jsonArray += ithJsonEntry;
            }
            // Remove trailing comma
            jsonArray = jsonArray.Remove(jsonArray.Length - 1, 1);
            return jsonArray;
        }

    }
}
