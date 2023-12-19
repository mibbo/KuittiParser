using System;
using System.Collections.Generic;
using Newtonsoft.Json;


namespace KuittiBot.Functions.Domain.Models
{

    public class FormRecognizerLabelDocument
    {
        [JsonProperty("$schema")]
        public string Schema { get; set; }

        [JsonProperty("document")]
        public string Document { get; set; }

        [JsonProperty("labels")]
        public List<Label> Labels { get; set; }
    }

    public class Label
    {
        [JsonProperty("label")]
        public string LabelName { get; set; }

        [JsonProperty("value")]
        public List<Value> Value { get; set; }

        [JsonProperty("labelType")]
        public string LabelType { get; set; }
    }

    public class Value
    {
        [JsonProperty("boundingBoxes")]
        public List<List<double>> BoundingBoxes { get; set; }

        [JsonProperty("page")]
        public int Page { get; set; }

        [JsonProperty("text")]
        public string Text { get; set; }
    }
}
