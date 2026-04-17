using Newtonsoft.Json;
using System;
using SyZero.Serialization;

namespace SyZero.Web.Common
{
    public class JsonSerialize : IJsonSerialize
    {
        public T JSONToObject<T>(string jsonText)
        {
            try
            {
                return JsonConvert.DeserializeObject<T>(jsonText);
            }
            catch (Exception ex)
            {
                throw new Exception("JSONHelper.JSONToObject(): " + ex.Message, ex);
            }
        }

        public string ObjectToJSON(object obj)
        {
            try
            {
                return JsonConvert.SerializeObject(obj);
            }
            catch (Exception ex)
            {
                throw new Exception("JSONHelper.ObjectToJSON(): " + ex.Message, ex);
            }
        }
    }
}
