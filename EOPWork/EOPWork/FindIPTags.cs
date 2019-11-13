using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;
using static System.Console;


namespace EOPWork
{
    class FindIPTags : IApplet
    {
        List<string> tagNames = new List<string>();

        public int Run(string[] args)
        {
            var dir = args[0];
            Process(dir);

            WriteLine("==============================");
            tagNames.Sort();
            tagNames.ForEach((name_) => WriteLine($"{name_}"));
            return 0;
        }

        void Process(string dir)
        {
            WriteLine($"Target dir: {dir}");
            foreach (var filename in Directory.GetFiles(dir, "*.xml"))
            {
                ProcessFile(filename);
            }
        }

        public void ProcessFile(string filename)
        {
            WriteLine($"**{filename}");
            var xd = XDocument.Load(filename);
            WalkNode_(xd.Root);

            void WalkNode_(XElement node)
            {
                SearchIPRange_(node);
                foreach (var child in node.Nodes())
                {
                    if (child is XElement elem)
                        WalkNode_(elem);
                }
            }

            void SearchIPRange_(XElement element)
            {
                foreach (var attr in element.Attributes())
                {
                    if (ValidateAttr_(attr))
                    {
                        if (!tagNames.Contains(attr.Name.LocalName))
                            tagNames.Add(attr.Name.LocalName);
                        WriteLine($"<{element.Name} {attr.Name.LocalName}=\"{attr.Value}\"/>");
                    }
                }
            }

            bool ValidateAttr_(XAttribute attr)
            {
                var name = attr.Name.LocalName;
                if (name.Contains("_IPV4") || name.Contains("IPV6") || name.EndsWith("_ip"))
                {
                    if (!attr.Value.Contains("-") && 
                        (attr.Value.Contains(".") || attr.Value.Contains(":")))
                    {
                        return true;
                    }
                }
                return false;
            }
        }
    }
}
