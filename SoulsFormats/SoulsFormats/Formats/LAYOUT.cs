using System;
using System.Collections.Generic;
using System.Xml;

namespace SoulsFormats
{
    /// <summary>
    /// Represents an XML Texture Atlas file and its SubTextures, file extension .layout
    /// </summary>
    public class TextureAtlas : SoulsFile<TextureAtlas>
    {
        /// <summary>
        /// List of SubTextures, sorted by name
        /// </summary>
        public List<SubTexture> SubTextures = new();
        /// <summary>
        /// Filename corresponding to a texture file. Textures are usually stored as .DDS files within a TPF binder, but the imagepath will typically end in .PNG or whatever the raw image format is
        /// </summary>
        public string ImagePath { get; set; }
        /// <summary>
        /// Full path of this particular .layout file relative to its binder. Only relevant for repacking.
        /// </summary>
        public string FileName { get; set; }
        /// <summary>
        /// Creates an empty TextureAtlas
        /// </summary>
        public TextureAtlas() { }
        /// <summary>
        /// Creates a TextureAtlas directly from its corresponding binderFile
        /// </summary>
        public TextureAtlas(BinderFile binderFile)
        {
            FileName = binderFile.Name;
            XmlDocument xmlDocument = new();
            xmlDocument.Load(SFUtil.GetDecompressedBR(new BinaryReaderEx(false, binderFile.Bytes), out _).Stream);
            ImagePath = xmlDocument.DocumentElement.Attributes.GetNamedItem("imagePath").Value;
            foreach (XmlNode subtextureNode in xmlDocument.DocumentElement.ChildNodes)
            {
                SubTextures.Add(new SubTexture(subtextureNode));
            }
            SubTextures.Sort();
        }
        /// <summary>
        /// Creates a TextureAtlas from its filename and an XML data string. The data string must be a valid XML format TextureAtlas or an Exception will be thrown.
        /// </summary>
        public TextureAtlas(string filename, string data)
        {
            FileName = filename;
            XmlDocument xmlDocument = new();
            xmlDocument.LoadXml(data);
            ImagePath = xmlDocument.DocumentElement.Attributes.GetNamedItem("imagePath").Value;
            foreach(XmlNode subtextureNode in xmlDocument.DocumentElement.ChildNodes)
            {
                SubTextures.Add(new SubTexture(subtextureNode));
            }
            SubTextures.Sort();
        }
        protected override bool Is(BinaryReaderEx br)
        {
            XmlDocument xmldoc = new();
            try
            {
                xmldoc.Load(br.Stream);
                if (xmldoc.DocumentElement.Name == "TextureAtlas") return true;
            }
            catch (Exception)
            {
                return false;
            }
            return false;
        }
        protected override void Read(BinaryReaderEx br)
        {
            XmlDocument xmldoc = new();
            xmldoc.Load(br.Stream);
            ImagePath = xmldoc.DocumentElement.Attributes.GetNamedItem("imagePath").Value;
            foreach (XmlNode subtextureNode in xmldoc.DocumentElement.ChildNodes)
            {
                SubTextures.Add(new SubTexture(subtextureNode));
            }
            SubTextures.Sort();
        }
        protected override void Write(BinaryWriterEx bw)
        {
            bw.WriteUTF16(XML());
        }
        /// <summary>
        /// Searches this TextureAtlas' SubTextures for one that matches the name of the one provided.
        /// Returns null if not found.
        /// </summary>
        public SubTexture Find(SubTexture subTexture)
        {
            return Find(subTexture.Name);
        }
        /// <summary>
        /// Searches this TextureAtlas' SubTextures for one that matches the name of the one provided.
        /// Returns null if not found.
        /// </summary>
        public SubTexture Find(string subTextureName)
        {
            foreach (SubTexture subTexture in SubTextures)
            {
                if (subTextureName.ToLower() == subTexture.Name.ToLower()) return subTexture;
            }
            return null;
        }
        /// <summary>
        /// Returns the full XML representation of this TextureAtlas and its SubTextures.
        /// </summary>
        public string XML()
        {
            string xml = $@"<TextureAtlas imagePath=""{ImagePath}"">{"\n"}";
            foreach (SubTexture subTexture in SubTextures)
            {
                xml += "\t" + subTexture.XML() + "\n";
            }
            xml += "</TextureAtlas>\n";
            return xml;
        }
        public override string ToString()
        {
            return XML();
        }
        /// <summary>
        /// A fully comparable SubTexture
        /// </summary>
        public class SubTexture : IComparable<SubTexture>
        {
            /// <summary>
            /// Name of the image file to be created by this SubTexture entry. Must be unique.
            /// </summary>
            public string Name { get; set; }
            /// <summary>
            /// The X coordinate corresponding to the top left corner of this SubTexture relative to its Texture.
            /// </summary>
            public int XCoord { get; set; }
            /// <summary>
            /// The Y coordinate corresponding to the top left corner of this SubTexture relative to its Texture.
            /// </summary>
            public int YCoord { get; set; }
            /// <summary>
            /// The width of this SubTexture, in pixels.
            /// </summary>
            public int Width { get; set; }
            /// <summary>
            /// The height of this SubTexture, in pixels.
            /// </summary>
            public int Height { get; set; }
            public int Half { get; set; }
            /// <summary>
            /// Creates a SubTexture from its base values.
            /// </summary>
            public SubTexture(string name, int x, int y, int width, int height, int half)
            {
                this.Name = name;
                this.XCoord = x;
                this.YCoord = y;
                this.Width = width;
                this.Height = height;
                this.Half = half;
            }
            /// <summary>
            /// Creates a SubTexture from its base values. Assumes the "half" to be 0.
            /// </summary>
            public SubTexture(string name, int x, int y, int width, int height)
            {
                this.Name = name;
                this.XCoord = x;
                this.YCoord = y;
                this.Width = width;
                this.Height = height;
                Half = 0;
            }
            /// <summary>
            /// Creates a SubTexture directly from an XmlNode. Will throw exceptions if the XML does not represent a SubTexture, or if it cannot parse any of the base values to integers.
            /// </summary>
            public SubTexture(XmlNode subtextureNode)
            {
                Name = subtextureNode.Attributes.GetNamedItem("name").Value;
                XCoord = int.Parse(subtextureNode.Attributes.GetNamedItem("x").Value);
                YCoord = int.Parse(subtextureNode.Attributes.GetNamedItem("y").Value);
                Width = int.Parse(subtextureNode.Attributes.GetNamedItem("width").Value);
                Height = int.Parse(subtextureNode.Attributes.GetNamedItem("height").Value);
                Half = int.Parse(subtextureNode.Attributes.GetNamedItem("half").Value);
            }
            /// <summary>
            /// Returns an XML representation of just this SubTexture
            /// </summary>
            public string XML()
            {
                return $@"<SubTexture name=""{Name}"" x=""{XCoord}"" y=""{YCoord}"" width=""{Width}"" height=""{Height}"" half=""{Half}""/>";
            }
            public override string ToString()
            {
                return XML();
            }
            /// <summary>
            /// Returns true if the given subtexture perfectly matches this one. False otherwise.
            /// </summary>
            public bool Equals(SubTexture subTexture)
            {
                if (Name != subTexture.Name) return false;
                if (XCoord != subTexture.XCoord) return false;
                if (YCoord != subTexture.YCoord) return false;
                if (Width != subTexture.Width) return false;
                if (Height != subTexture.Height) return false;
                if (Half != subTexture.Half) return false;
                return true;
            }
            /// <summary>
            /// Compares to other SubTextures by their name parameters for sorting purposes.
            /// </summary>
            public int CompareTo(SubTexture other)
            {
                return Name.ToLower().CompareTo(other.Name.ToLower());
            }
        }
    }
}
