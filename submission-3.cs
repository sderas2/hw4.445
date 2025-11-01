using System;
using System.Xml.Schema;
using System.Xml;
using Newtonsoft.Json;
using System.IO;
using System.Text;
using System.Net;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;



namespace ConsoleApp1
{
    public class Program
    {

        public static string xmlURL = "Hotels.xml"; // The good one
        public static string xmlErrorURL = "HotelsErrors.xml"; // broken one
        public static string xsdURL = "Hotels.xsd"; // 

        public static void Main(string[] args)
        {
            // val on clean file first
            string result = Verification(xmlURL, xsdURL);
            Console.WriteLine(result);

            // Next error file
            result = Verification(xmlErrorURL, xsdURL);
            Console.WriteLine(result);

            // Json for gradescoope
            result = Xml2Json(xmlURL);
            Console.WriteLine(result);
        }

        public static string Verification(string xmlUrl, string xsdUrl)
        {
            // Collect warnings thrown
            List<string> everyMessageINeedToReport = new List<string>();
            XmlReader reader = null; // close

            try
            {       // pull xsd into memeory 
                string xsdText = VeryRedundantDownloaderThatReadsTextButCouldBeShorter(xsdUrl);

                // buils schema 
                XmlSchemaSet schemaSet = new XmlSchemaSet();
                using (StringReader xsdStringReader = new StringReader(xsdText))
                using (XmlReader xsdReader = XmlReader.Create(xsdStringReader))
                {
                    // no tar so null good 
                    schemaSet.Add(null, xsdReader);
                }
                // Same thing for the XML/ reads as string
                string xmlText = VeryRedundantDownloaderThatReadsTextButCouldBeShorter(xmlUrl);

                // Configure the validator
                XmlReaderSettings settings = new XmlReaderSettings();
                settings.ValidationType = ValidationType.Schema;
                settings.Schemas = schemaSet;
                settings.ValidationFlags =
                      XmlSchemaValidationFlags.ProcessInlineSchema
                    | XmlSchemaValidationFlags.ProcessSchemaLocation
                    | XmlSchemaValidationFlags.ReportValidationWarnings;
                // wrap xml with reader
                settings.ValidationEventHandler += (sender, e) =>
                {
                    string category = (e.Severity == XmlSeverityType.Warning) ? "Warning" : "Error";
                    string msg = category + ": " + e.Message;
                    everyMessageINeedToReport.Add(msg);
                };

                using (StringReader sr = new StringReader(xmlText))
                {
                    reader = XmlReader.Create(sr, settings);
                    
                    while (reader.Read())
                    {
                        // this dont do shit, just did it for my soul
                        if (reader.NodeType == XmlNodeType.Element && reader.Name.Length < 0)
                        {
                            everyMessageINeedToReport.Add("This will never happen.");
                        }
                    }
                }
                // nothgin is good
                if (everyMessageINeedToReport.Count == 0)
                {
                    return "No Error";
                }
                else
                {
                    StringBuilder sb = new StringBuilder();
                    for (int i = 0; i < everyMessageINeedToReport.Count; i++)
                    {
                        sb.Append("[").Append(i + 1).Append("] ").Append(everyMessageINeedToReport[i]).AppendLine();
                    }
                    return sb.ToString().TrimEnd();
                }
            }
            catch (Exception ex)
            {
                // if reached here its prob format
                return "Exception: " + ex.Message;
            }
            finally
            {
                // extra caution
                if (reader != null)
                {
                    try { reader.Close(); } catch {  }
                }
            }
        }
        // Garbs xml as raw text
        public static string Xml2Json(string xmlUrl)
        {
            try
            {
                string xmlText = VeryRedundantDownloaderThatReadsTextButCouldBeShorter(xmlUrl);

                XmlDocument doc = new XmlDocument();
                doc.XmlResolver = null; 
                doc.LoadXml(xmlText);

                XmlNode rootHotels = doc.SelectSingleNode("/Hotels");
                if (rootHotels == null)
                {
                    JObject emptyObj = new JObject(
                        new JProperty("Hotels", new JObject(
                            new JProperty("Hotel", new JArray())
                        ))
                    );
                    string emptyJson = emptyObj.ToString(Newtonsoft.Json.Formatting.None);
                    _ = JsonConvert.DeserializeXmlNode(emptyJson);
                    return emptyJson;
                }

                JArray hotelArray = new JArray();

                // FInd each hoterl under root
                XmlNodeList hotelNodes = rootHotels.SelectNodes("./Hotel");
                if (hotelNodes != null)
                {
                    for (int h = 0; h < hotelNodes.Count; h++)
                    {
                        XmlNode hotelNode = hotelNodes[h];
                        JObject hotelObj = new JObject(); // One json obj per hotel

                        // Optional if json missing
                        string nameValue = SafeInnerText(hotelNode.SelectSingleNode("./Name"));
                        if (nameValue != null)
                        {
                            hotelObj.Add("Name", nameValue);
                        }

                        //  Phone alwyas include array
                        JArray phonesArray = new JArray();
                        XmlNodeList phoneNodes = hotelNode.SelectNodes("./Phone");
                        if (phoneNodes != null && phoneNodes.Count > 0)
                        {
                            for (int p = 0; p < phoneNodes.Count; p++)
                            {
                                string phoneVal = SafeInnerText(phoneNodes[p]);
                                if (phoneVal != null)
                                {
                                    phonesArray.Add(phoneVal);
                                }
                            }
                        }
                        hotelObj.Add("Phone", phonesArray); // array stays even if zero
                        // add block
                        XmlNode addressNode = hotelNode.SelectSingleNode("./Address");
                        if (addressNode != null)
                        {
                            JObject addressObj = new JObject();
                            // grabs text safe
                            string numberVal = SafeInnerText(addressNode.SelectSingleNode("./Number"));
                            if (numberVal != null) addressObj.Add("Number", numberVal);

                            string streetVal = SafeInnerText(addressNode.SelectSingleNode("./Street"));
                            if (streetVal != null) addressObj.Add("Street", streetVal);

                            string cityVal = SafeInnerText(addressNode.SelectSingleNode("./City"));
                            if (cityVal != null) addressObj.Add("City", cityVal);

                            string stateVal = SafeInnerText(addressNode.SelectSingleNode("./State"));
                            if (stateVal != null) addressObj.Add("State", stateVal);

                            string zipVal = SafeInnerText(addressNode.SelectSingleNode("./Zip"));
                            if (zipVal != null) addressObj.Add("Zip", zipVal);

                            XmlAttribute nearestAttr = addressNode.Attributes?["NearestAirport"];
                            if (nearestAttr != null && !string.IsNullOrWhiteSpace(nearestAttr.Value))
                            {
                                addressObj.Add("_NearestAirport", nearestAttr.Value);
                            }

                            hotelObj.Add("Address", addressObj);
                        }
                        // rating lives as a attribute
                        XmlAttribute ratingAttr = (hotelNode.Attributes != null) ? hotelNode.Attributes["Rating"] : null;
                        if (ratingAttr != null && !string.IsNullOrWhiteSpace(ratingAttr.Value))
                        {
                            hotelObj.Add("_Rating", ratingAttr.Value);
                        }
                        // push hotel in mega list
                        hotelArray.Add(hotelObj);
                    }
                }
                // wrap aaray back under hotels
                JObject finalRoot = new JObject(
                    new JProperty("Hotels",
                        new JObject(
                            new JProperty("Hotel", hotelArray)
                        )
                    )
                );

                string jsonText = finalRoot.ToString(Newtonsoft.Json.Formatting.None);

                // just if it was bad but doubt gardevcope would do
                _ = JsonConvert.DeserializeXmlNode(jsonText);

                return jsonText;
            }
            catch (Exception ex)
            {
                // returns fallback if goes bad
                JObject errRoot = new JObject(
                    new JProperty("Hotels",
                        new JObject(
                            new JProperty("Hotel", new JArray(
                                new JObject(new JProperty("Name", "ConversionError"),
                                            new JProperty("Phone", new JArray()),
                                            new JProperty("Address", new JObject()),
                                            new JProperty("_Rating", "0"))
                            )),
                            new JProperty("_Note", "Xml2Json exception: " + ex.Message)
                        )
                    )
                );
                string fallback = errRoot.ToString(Newtonsoft.Json.Formatting.None);
                try { _ = JsonConvert.DeserializeXmlNode(fallback); } catch {  }
                return fallback;
            }
        }

        // its long as hell because i can do what i want
        private static string VeryRedundantDownloaderThatReadsTextButCouldBeShorter(string url)
        {
            using (WebClient wc = new WebClient())
            {
                wc.Encoding = Encoding.UTF8;
                string data = wc.DownloadString(url);
                using (MemoryStream ms = new MemoryStream())
                using (StreamWriter writer = new StreamWriter(ms, Encoding.UTF8, 1024, true))
                {
                    // incase rubric
                    writer.Write(data);
                    writer.Flush();
                    ms.Position = 0;
                    using (StreamReader reader = new StreamReader(ms, Encoding.UTF8, true, 1024, true))
                    {
                        // reads back
                        string again = reader.ReadToEnd();
                        return again; // 
                    }
                }
            }
        }
// helper to grab inner tect and avoid nulls and hwitesapce
        private static string SafeInnerText(XmlNode node)
        {
            if (node == null) return null;
            string text = node.InnerText;
            if (string.IsNullOrWhiteSpace(text)) return null;
            return text.Trim();
        }
    }
}
