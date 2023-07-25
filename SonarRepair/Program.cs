// See https://aka.ms/new-console-template for more information
using Fare;
using SonarRepair;
using YamlDotNet.RepresentationModel;

Console.WriteLine("Yaml file path : ");
string? path = Console.ReadLine();

while (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
{
    Console.WriteLine("Yaml file path : ");
    path = Console.ReadLine();
}

string? contents = File.ReadAllText(path);
using var sr = new StringReader(contents);

var yaml = new YamlStream();
yaml.Load(sr);

var root = (YamlMappingNode)yaml.Documents[0].RootNode;

YamlMappingNode components = (YamlMappingNode)root.Children[new YamlScalarNode("components")];
if (components is null)
{
    Console.WriteLine("yaml file doesn't have components.");
    return;
}

YamlMappingNode schemas = (YamlMappingNode)components!.Children[new YamlScalarNode("schemas")];
if (schemas is null)
{
    Console.WriteLine("yaml file doesn't have schemas.");
    return;
}

ProcessYaml(schemas, schemas);


using (TextWriter writer = File.CreateText(path.Replace(".yml", "V2.yml")))
{
    yaml.Save(writer, false);
}

static void ProcessYaml(YamlMappingNode nodes, YamlMappingNode schemas)
{
    foreach (var node in nodes!.Children)
    {
        YamlMappingNode? objVal = (YamlMappingNode?)node.Value;
        if (objVal is null)
        {
            continue;
        }

        string type = objVal.Children.ContainsKey("type") ? objVal.Children["type"].ToString() : "href";

        if (!objVal.Children.ContainsKey("description") && type != "href")
        {
            YamlScalarNode descNode = new("description")
            {
                Value = Helper.SplitCamelCase(node.Key.ToString())?.ToLower(),
                Style = YamlDotNet.Core.ScalarStyle.DoubleQuoted
            };

            objVal.Children.Add("description", descNode);
        }

        if (type == "object")
        {
            var props = (YamlMappingNode)objVal!.Children[new YamlScalarNode("properties")];
            if (props is not null)
            {
                ProcessYaml(props, schemas);
            }

            SetObjectExample(props, objVal, schemas, type);
        }
        else
        {
            if (!objVal.Children.ContainsKey("example"))
            {
                string value = "test";
                YamlDotNet.Core.ScalarStyle style = YamlDotNet.Core.ScalarStyle.Plain;

                if (objVal.Children.ContainsKey("format") && objVal.Children["format"].ToString() == "date-time")
                {
                    value = DateTime.Now.ToString("yyyy-MM-ddTHH:mm:ssZ");
                    style = YamlDotNet.Core.ScalarStyle.DoubleQuoted;
                }
                else
                {
                    switch (type)
                    {
                        case "string":
                            if (objVal.Children.ContainsKey("enum"))
                            {
                                value = objVal.Children["enum"][0].ToString();
                            }
                            else if (objVal.Children.ContainsKey("pattern"))
                            {
                                try
                                {
                                    string pattern = objVal.Children["pattern"].ToString();
                                    var xeger = new Xeger(pattern);
                                    value = xeger.Generate();
                                }
                                catch
                                {
                                    value = objVal["description"].ToString();
                                }
                            }
                            else
                            {
                                value = objVal["description"].ToString();
                            }

                            style = YamlDotNet.Core.ScalarStyle.DoubleQuoted;
                            break;

                        case "integer":
                        case "number":
                            if (objVal.Children.ContainsKey("enum"))
                            {
                                value = objVal.Children["enum"][0].ToString();
                            }
                            else
                            {
                                value = $"{Helper.GenerateNumber()}";
                            }

                            break;

                        case "boolean":
                            value = "true";
                            break;

                        case "array":
                            YamlMappingNode items = (YamlMappingNode)objVal.Children["items"];
                            if (items is null)
                            {
                                continue;
                            }
                            if (items.Children.ContainsKey("type"))
                            {
                                string itemType = items.Children["type"].ToString();
                                switch (itemType)
                                {
                                    case "string":
                                        if (items.Children.ContainsKey("enum"))
                                        {
                                            value = items.Children["enum"][0].ToString();
                                        }
                                        else if (items.Children.ContainsKey("pattern"))
                                        {
                                            try
                                            {
                                                string pattern = objVal.Children["pattern"].ToString();
                                                var xeger = new Xeger(pattern);
                                                value = xeger.Generate();
                                            }
                                            catch
                                            {
                                                value = objVal["description"].ToString();
                                            }
                                        }
                                        else
                                        {
                                            value = objVal["description"].ToString();
                                        }

                                        style = YamlDotNet.Core.ScalarStyle.DoubleQuoted;
                                        break;

                                    case "integer":
                                    case "number":
                                        if (items.Children.ContainsKey("enum"))
                                        {
                                            value = items.Children["enum"][0].ToString();
                                        }
                                        else
                                        {
                                            value = $"{Helper.GenerateNumber()}";
                                        }

                                        break;

                                    case "boolean":
                                        value = "true";
                                        break;
                                    default:
                                        continue;
                                }
                            }
                            else
                            {
                                string? refType = items?["$ref"]?.ToString();
                                if (string.IsNullOrWhiteSpace(refType))
                                {
                                    continue;
                                }

                                string typeName = refType.Substring(refType.LastIndexOf("/") + 1, refType.Length - refType.LastIndexOf("/") - 1);
                                if (!schemas!.Children.ContainsKey(typeName))
                                {
                                    continue;
                                }

                                YamlMappingNode expObj = (YamlMappingNode)schemas!.Children[new YamlScalarNode(typeName)];
                                var expObjBaseProps = (YamlMappingNode)expObj!.Children[new YamlScalarNode("properties")];
                                if (expObjBaseProps is not null)
                                {
                                    SetObjectExample(expObjBaseProps, objVal, schemas, type);
                                    continue;
                                }
                                break;
                            }
                            break;
                        case "href":
                            //ref can"t have any other properties besides it like: description, example , ...
                            continue;
                            //YamlScalarNode comp = (YamlScalarNode)objVal.Children["$ref"];
                            //if (comp is null)
                            //{
                            //    continue;
                            //}

                            //string? refType2 = comp.ToString();
                            //if (string.IsNullOrWhiteSpace(refType2))
                            //{
                            //    continue;
                            //}

                            //string typeName2 = refType2.Substring(refType2.LastIndexOf("/") + 1, refType2.Length - refType2.LastIndexOf("/") - 1);
                            //if (!schemas!.Children.ContainsKey(typeName2))
                            //{
                            //    continue;
                            //}

                            //YamlMappingNode expObj2 = (YamlMappingNode)schemas!.Children[new YamlScalarNode(typeName2)];
                            //var expObjBaseProps2 = (YamlMappingNode)expObj2!.Children[new YamlScalarNode("properties")];
                            //if (expObjBaseProps2 is not null)
                            //{
                            //    SetObjectExample(expObjBaseProps2, objVal, schemas, type);
                            //    continue;
                            //}
                            //break;
                    }
                }

                YamlScalarNode descNode = new()
                {
                    Value = value,
                    Style = style
                };

                if (type == "array")
                {
                    YamlSequenceNode seq = new YamlSequenceNode { descNode };
                    objVal.Children.Add("example", seq);
                }
                else
                {
                    objVal.Children.Add("example", descNode);
                }
            }
        }
    }
}

static void SetObjectExample(YamlMappingNode? props, YamlMappingNode? objVal, YamlMappingNode schemas, string type)
{
    //object example
    if (!objVal.Children.ContainsKey("example"))
    {
        Dictionary<YamlNode, YamlNode> nds = new();
        foreach (var e in props.Children)
        {
            string expType = e.Value.ToString().Contains("type") ? e.Value["type"].ToString() : "href";
            string value = "test";
            YamlDotNet.Core.ScalarStyle style = YamlDotNet.Core.ScalarStyle.Plain;

            if (e.Value.ToString().Contains("{ format,") && e.Value["format"].ToString() == "date-time")
            {
                value = DateTime.Now.ToString("yyyy-MM-ddTHH:mm:ssZ");
                style = YamlDotNet.Core.ScalarStyle.DoubleQuoted;
            }
            else
            {
                switch (expType)
                {
                    case "string":
                        if (e.Value.ToString().Contains("enum"))
                        {
                            value = e.Value["enum"][0].ToString();
                        }
                        else if (e.Value.ToString().Contains("pattern"))
                        {
                            try
                            {
                                string pattern = e.Value["pattern"].ToString();
                                var xeger = new Xeger(pattern);
                                value = xeger.Generate();
                            }
                            catch
                            {
                                value = $"sample-{e.Key}";
                            }
                        }
                        else
                        {
                            value = $"sample-{e.Key}";
                        }

                        style = YamlDotNet.Core.ScalarStyle.DoubleQuoted;
                        break;

                    case "integer":
                    case "number":
                        if (e.Value.ToString().Contains("enum"))
                        {
                            value = e.Value["enum"][0].ToString();
                        }
                        else
                        {
                            value = $"{Helper.GenerateNumber()}";
                        }

                        break;

                    case "boolean":
                        value = "true";
                        break;

                    case "array":
                        YamlMappingNode items = (YamlMappingNode)e.Value["items"];
                        if (items is null)
                        {
                            continue;
                        }
                        if (items.Children.ContainsKey("type"))
                        {
                            string itemType = items.Children["type"].ToString();
                            switch (itemType)
                            {
                                case "string":
                                    if (e.Value.ToString().Contains("enum"))
                                    {
                                        value = e.Value["enum"][0].ToString();
                                    }
                                    else if (e.Value.ToString().Contains("pattern"))
                                    {
                                        try
                                        {
                                            string pattern = e.Value["pattern"].ToString();
                                            var xeger = new Xeger(pattern);
                                            value = xeger.Generate();
                                        }
                                        catch
                                        {
                                            value = $"sample-{e.Key}";
                                        }
                                    }
                                    else
                                    {
                                        value = $"sample-{e.Key}";
                                    }

                                    style = YamlDotNet.Core.ScalarStyle.DoubleQuoted;
                                    break;

                                case "integer":
                                case "number":
                                    if (e.Value.ToString().Contains("enum"))
                                    {
                                        value = e.Value["enum"][0].ToString();
                                    }
                                    else
                                    {
                                        value = $"{Helper.GenerateNumber()}";
                                    }

                                    break;

                                case "boolean":
                                    value = "true";
                                    break;
                                default:
                                    continue;
                            }

                            YamlScalarNode scalNode = new()
                            {
                                Value = value,
                                Style = style
                            };
                            YamlSequenceNode seq = new YamlSequenceNode { scalNode };
                            nds.Add(e.Key.ToString(), seq);
                            continue;
                        }
                        else
                        {
                            string? refType = e.Value["items"]?["$ref"]?.ToString();
                            if (string.IsNullOrWhiteSpace(refType))
                            {
                                continue;
                            }

                            string typeName = refType.Substring(refType.LastIndexOf("/") + 1, refType.Length - refType.LastIndexOf("/") - 1);
                            if (!schemas!.Children.ContainsKey(typeName))
                            {
                                continue;
                            }

                            YamlMappingNode expObj = (YamlMappingNode)schemas!.Children[new YamlScalarNode(typeName)];
                            var expObjBaseProps = (YamlMappingNode)expObj!.Children[new YamlScalarNode("properties")];
                            if (expObjBaseProps is not null)
                            {
                                YamlSequenceNode seq = new YamlSequenceNode { ConstructExample(expObjBaseProps, schemas, type) };
                                nds.Add(e.Key.ToString(), seq);
                                continue;
                            }
                            break;
                        }
                    case "href":
                        //ref can"t have any other properties besides it like: description, example , ...
                        continue;
                        //YamlScalarNode comp = (YamlScalarNode)e.Value["$ref"];
                        //if (comp is null)
                        //{
                        //    continue;
                        //}

                        //string? refType2 = comp.ToString();
                        //if (string.IsNullOrWhiteSpace(refType2))
                        //{
                        //    continue;
                        //}

                        //string typeName2 = refType2.Substring(refType2.LastIndexOf("/") + 1, refType2.Length - refType2.LastIndexOf("/") - 1);
                        //if (!schemas!.Children.ContainsKey(typeName2))
                        //{
                        //    continue;
                        //}

                        //YamlMappingNode expObj2 = (YamlMappingNode)schemas!.Children[new YamlScalarNode(typeName2)];
                        //var expObjBaseProps2 = (YamlMappingNode)expObj2!.Children[new YamlScalarNode("properties")];
                        //if (expObjBaseProps2 is not null)
                        //{
                        //    nds.Add(e.Key.ToString(), ConstructExample(expObjBaseProps2, schemas, type));
                        //    continue;
                        //}
                        //break;
                }
            }

            YamlScalarNode expNode = new()
            {
                Value = value,
                Style = style
            };
            nds.Add(e.Key.ToString(), expNode);
        }

        YamlMappingNode descNode = new(nds);

        if (type == "array")
        {
            YamlSequenceNode seq = new YamlSequenceNode { descNode };
            objVal.Children.Add("example", seq);
        }
        else
        {
            objVal.Children.Add("example", descNode);
        }
    }
}

static YamlMappingNode ConstructExample(YamlMappingNode? props, YamlMappingNode schemas, string type)
{
    //object example
    YamlMappingNode reslt = new();
    foreach (var e in props.Children)
    {
        string expType = e.Value.ToString().Contains("type") ? e.Value["type"].ToString() : "href";
        string value = "test";
        YamlDotNet.Core.ScalarStyle style = YamlDotNet.Core.ScalarStyle.Plain;

        if (e.Value.ToString().Contains("{ format,") && e.Value["format"].ToString() == "date-time")
        {
            value = DateTime.Now.ToString("yyyy-MM-ddTHH:mm:ssZ");
            style = YamlDotNet.Core.ScalarStyle.DoubleQuoted;
        }
        else
        {
            switch (expType)
            {
                case "string":
                    if (e.Value.ToString().Contains("enum"))
                    {
                        value = e.Value["enum"][0].ToString();
                    }
                    else if (e.Value.ToString().Contains("pattern"))
                    {
                        try
                        {
                            string pattern = e.Value["pattern"].ToString();
                            var xeger = new Xeger(pattern);
                            value = xeger.Generate();
                        }
                        catch
                        {
                            value = $"sample-{e.Key}";
                        }
                    }
                    else
                    {
                        value = $"sample-{e.Key}";
                    }

                    style = YamlDotNet.Core.ScalarStyle.DoubleQuoted;
                    break;

                case "integer":
                case "number":
                    if (e.Value.ToString().Contains("enum"))
                    {
                        value = e.Value["enum"][0].ToString();
                    }
                    else
                    {
                        value = $"{Helper.GenerateNumber()}";
                    }

                    break;

                case "boolean":
                    value = "true";
                    break;

                case "array":
                    YamlMappingNode items = (YamlMappingNode)e.Value["items"];
                    if (items is null)
                    {
                        continue;
                    }
                    if (items.Children.ContainsKey("type"))
                    {
                        string itemType = items.Children["type"].ToString();
                        switch (itemType)
                        {
                            case "string":
                                if (e.Value.ToString().Contains("enum"))
                                {
                                    value = e.Value["enum"][0].ToString();
                                }
                                else if (e.Value.ToString().Contains("pattern"))
                                {
                                    try
                                    {
                                        string pattern = e.Value["pattern"].ToString();
                                        var xeger = new Xeger(pattern);
                                        value = xeger.Generate();
                                    }
                                    catch
                                    {
                                        value = $"sample-{e.Key}";
                                    }
                                }
                                else
                                {
                                    value = $"sample-{e.Key}";
                                }

                                style = YamlDotNet.Core.ScalarStyle.DoubleQuoted;
                                break;

                            case "integer":
                            case "number":
                                if (e.Value.ToString().Contains("enum"))
                                {
                                    value = e.Value["enum"][0].ToString();
                                }
                                else
                                {
                                    value = $"{Helper.GenerateNumber()}";
                                }

                                break;

                            case "boolean":
                                value = "true";
                                break;
                            default:
                                continue;
                        }

                        YamlScalarNode scalNode = new()
                        {
                            Value = value,
                            Style = style
                        };
                        YamlSequenceNode seq = new YamlSequenceNode { scalNode };
                        reslt.Add(e.Key.ToString(), seq);
                        continue;
                    }
                    else
                    {
                        string? refType = e.Value["items"]?["$ref"]?.ToString();
                        if (string.IsNullOrWhiteSpace(refType))
                        {
                            continue;
                        }

                        string typeName = refType.Substring(refType.LastIndexOf("/") + 1, refType.Length - refType.LastIndexOf("/") - 1);
                        if (!schemas!.Children.ContainsKey(typeName))
                        {
                            continue;
                        }

                        YamlMappingNode expObj = (YamlMappingNode)schemas!.Children[new YamlScalarNode(typeName)];
                        var expObjBaseProps = (YamlMappingNode)expObj!.Children[new YamlScalarNode("properties")];
                        if (expObjBaseProps is not null)
                        {
                            YamlSequenceNode seq = new YamlSequenceNode { ConstructExample(expObjBaseProps, schemas, type) };
                            reslt.Add(e.Key.ToString(), seq);
                            continue;
                        }
                        break;
                    }
                case "href":
                    //ref can"t have any other properties besides it like: description, example , ...
                    continue;
                    //YamlScalarNode comp = (YamlScalarNode)e.Value["$ref"];
                    //if (comp is null)
                    //{
                    //    continue;
                    //}

                    //string? refType2 = comp.ToString();
                    //if (string.IsNullOrWhiteSpace(refType2))
                    //{
                    //    continue;
                    //}

                    //string typeName2 = refType2.Substring(refType2.LastIndexOf("/") + 1, refType2.Length - refType2.LastIndexOf("/") - 1);
                    //if (!schemas!.Children.ContainsKey(typeName2))
                    //{
                    //    continue;
                    //}

                    //YamlMappingNode expObj2 = (YamlMappingNode)schemas!.Children[new YamlScalarNode(typeName2)];
                    //var expObjBaseProps2 = (YamlMappingNode)expObj2!.Children[new YamlScalarNode("properties")];
                    //if (expObjBaseProps2 is not null)
                    //{
                    //    reslt.Add(e.Key.ToString(), ConstructExample(expObjBaseProps2, schemas, type));
                    //    continue;
                    //}
                    //break;
            }
        }

        YamlScalarNode expNode = new()
        {
            Value = value,
            Style = style
        };

        reslt.Add(e.Key.ToString(), expNode);
    }

    return reslt;
}
