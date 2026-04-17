using System.Xml;

namespace SyZero.Web.Common
{
    public class XmlSerialize : IXmlSerialize
    {
        public bool AppendChild(string filePath, string xPath, XmlNode xmlNode)
        {
            if (xmlNode == null)
            {
                return false;
            }

            try
            {
                XmlDocument doc = new XmlDocument();
                doc.Load(filePath);
                XmlNode xn = doc.SelectSingleNode(xPath);
                if (xn == null)
                {
                    return false;
                }

                XmlNode n = doc.ImportNode(xmlNode, true);
                xn.AppendChild(n);
                doc.Save(filePath);
                return true;
            }
            catch
            {
                return false;
            }
        }

        public bool AppendChild(string filePath, string xPath, string toFilePath, string toXPath)
        {
            try
            {
                XmlDocument doc = new XmlDocument();
                doc.Load(toFilePath);
                XmlNode xn = doc.SelectSingleNode(toXPath);
                if (xn == null)
                {
                    return false;
                }

                XmlNodeList xnList = ReadNodes(filePath, xPath);
                if (xnList == null || xnList.Count == 0)
                {
                    return false;
                }

                foreach (XmlNode xe in xnList)
                {
                    XmlNode n = doc.ImportNode(xe, true);
                    xn.AppendChild(n);
                }
                doc.Save(toFilePath);
                return true;
            }
            catch
            {
                return false;
            }
        }

        public XmlDocument LoadXmlDoc(string filePath)
        {
            try
            {
                XmlDocument doc = new XmlDocument();
                doc.Load(filePath);
                return doc;
            }
            catch
            {
                return null;
            }
        }

        public XmlNodeList ReadNodes(string filePath, string xPath)
        {
            try
            {
                XmlDocument doc = new XmlDocument();
                doc.Load(filePath);
                XmlNode xn = doc.SelectSingleNode(xPath);
                if (xn == null)
                {
                    return null;
                }

                XmlNodeList xnList = xn.ChildNodes;
                return xnList;
            }
            catch
            {
                return null;
            }
        }

        public bool UpdateNodeInnerText(string filePath, string xPath, string value)
        {
            try
            {
                XmlDocument doc = new XmlDocument();
                doc.Load(filePath);
                XmlNode xn = doc.SelectSingleNode(xPath);
                if (xn == null)
                {
                    return false;
                }

                xn.InnerText = value;
                doc.Save(filePath);
            }
            catch
            {
                return false;
            }
            return true;
        }
    }
}
