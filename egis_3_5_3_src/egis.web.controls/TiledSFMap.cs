#region Copyright and License

/****************************************************************************
**
** Copyright (C) 2008 - 2011 Winston Fletcher.
** All rights reserved.
**
** This file is part of the EGIS.Web.controls class library of Easy GIS .NET.
** 
** Easy GIS .NET is free software: you can redistribute it and/or modify
** it under the terms of the GNU Lesser General Public License version 3 as
** published by the Free Software Foundation and appearing in the file
** lgpl-license.txt included in the packaging of this file.
**
** Easy GIS .NET is distributed in the hope that it will be useful,
** but WITHOUT ANY WARRANTY; without even the implied warranty of
** MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
** GNU General Public License for more details.
**
** You should have received a copy of the GNU General Public License and
** GNU Lesser General Public License along with Easy GIS .NET.
** If not, see <http://www.gnu.org/licenses/>.
**
****************************************************************************/

#endregion


using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Text;
using System.Web;
using System.Web.UI;
using System.Web.UI.WebControls;

using System.Drawing;
using System.Security.Permissions;
using System.Drawing.Design;
using System.Drawing.Imaging;
using System.Web.UI.Design;
using System.Configuration;
using System.Web.Configuration;
using System.Collections.Specialized;
using System.IO;
using System.Xml;
using System.Security.Cryptography;
using EGIS.ShapeFileLib;

namespace EGIS.Web.Controls
{

    /// <summary>
    /// ASP .NET Mapping Control which loads and displays an Easy GIS .NET map project in a web page using map tile images.
    /// </summary>
    /// <remarks>    
    /// <para>This control is similar to the SFMap Control, but it uses a tile approach to take advantage of caching on
    /// client's web-browsers to provide much faster map interaction.</para>
    /// <para>Note that in order to use the SFMap the displayed project must be using
    /// lat long degree coordinates as the map will use a Mercator Projection
    /// </para>
    /// <para>
    /// Tiles are organised in a manner similar to the approach used by google maps and bing maps. Each tiles dimension is 256x256 pixels.
    /// A tile request is made up of a zoom-level between 0 and 16(inclusive), tile x-ccord and a tile y-ccord. At zoom-level
    /// 0 the entire world (-180 lon -> +180 lon) is scaled to fit in 1 tile. At level 1 the world will fit
    /// in 2 tiles x 2 tiles, at level 2 the world will fit into 4 tiles x 4 tiles, .. etc.     
    /// </para>
    /// <para>Tiles are numbered from zero in the upper left corner to (NumTiles at zoom-level)-1 as below:</para>
    /// <para>
    /// <code>
    /// (0,0) (1,0) (2,0) ..
    /// (0,1) (1,1) (2,1) ..
    /// (0,2) (1,2) (2,2) ..
    /// ..
    /// </code>
    /// </para>
    /// <para>An Easy GIS .NET project is composed of a number of Shapefiles and can be designed using the Desktop Edition of 
    /// Easy GIS .NET. The project should be exported and copied to the web server hosting the page where the TiledSFMap Web Control will be used. For more 
    /// information on how to export a project ready to load in a web page see <a href = "http://www.easygisdotnet.com/api">Easy GIS .NET Developers page</a>
    /// The TiledSFMap Control generates all map images on the hosting web server using an IHttpHandler. All requests are performed automatically on the 
    /// client's web browser using JavaScript and AJAX, resulting in a map that can be panned and zoomed in or out without refreshing the
    /// web page for each request.
    /// </para>
    /// <para>    
    /// NOTE: In order for the server to render maps an entry in the httpHandlers section of the web.config file must be made.
    /// If the TiledSFMap Control is added to a page using the design view in Visual Studio an entry is added to the web.config file automatically, however
    /// if the Control is manually added to a page it will be neccessary to add the following section to the config file.
    /// <code>
    /// &lt;httpHandlers&gt;
    ///        &lt;add path="egismaptiled.axd" verb="*" type="EGIS.Web.Controls.SFMapImageProvider, EGIS.Web.Controls, Version=2.3.0.0, Culture=neutral, PublicKeyToken=05b98c869b5ffe6a"
    ///            validate="true" /&gt;
    ///    &lt;/httpHandlers&gt;
    /// </code>
    /// </para>
    /// </remarks>
    public class TiledSFMap : CompositeControl, ISFMap
    {

        /// <summary>
        /// Constructor
        /// </summary>
        public TiledSFMap()
        {
            this.Height = new Unit(100);
            this.Width = new Unit(150);            
        }

        
        /// <summary>
        /// overrides OnInit in WebControl
        /// </summary>
        /// <param name="e"></param>
        protected override void OnInit(EventArgs e)
        {
            base.OnInit(e);
            //CreateMap();
        }

        /// <summary>
        /// overrides the TagKey property. Returns a Div Tag.
        /// </summary>
        protected override HtmlTextWriterTag TagKey
        {
            get
            {
                return HtmlTextWriterTag.Div;
            }
        }


        #region private controls

        //need to add an image (for rendering) and a panel for mouse event panning
        //place the panel ontop of the image with no background set so we can see the image, but use the panel for mouse events
        private System.Web.UI.WebControls.Panel cntrlPanel;// = new Panel();
        private System.Web.UI.WebControls.Image gisImage;
        private System.Web.UI.WebControls.Panel eventPanel;
        private System.Web.UI.WebControls.HiddenField hfZoom = new HiddenField();
        private System.Web.UI.WebControls.HiddenField hfX = new HiddenField();
        private System.Web.UI.WebControls.HiddenField hfY = new HiddenField();
        private System.Web.UI.WebControls.HiddenField hfcoc;
        private System.Web.UI.WebControls.HiddenField hftooltipUrl;
        private System.Web.UI.WebControls.Panel fPnl = new Panel();

        #endregion

        internal String ZoomFieldClientId
        {
            get
            {
                return hfZoom.ClientID;
            }
        }

        internal String CenterXFieldClientId
        {
            get
            {
                return hfX.ClientID;
            }
        }

        internal String CenterYFieldClientId
        {
            get
            {
                return hfY.ClientID;
            }
        }

        internal string CacheOnClientFieldClientId
        {
            get
            {
                return this.hfcoc.ClientID;
            }
        }

        /// <summary>
        /// overrides CreateChildControls in CompositeControl
        /// </summary>
        protected override void CreateChildControls()
        {
            if (!this.DesignMode)
            {
                CreateMap();
                
                int ho = 0;                
                Unit h = new Unit(Math.Max(this.Height.Value - ho, 0));

                cntrlPanel = new Panel();
                cntrlPanel.Style[HtmlTextWriterStyle.Overflow] = "hidden";
                cntrlPanel.Width = this.Width;
                cntrlPanel.Height = this.Height;
                cntrlPanel.Style[HtmlTextWriterStyle.Position] = "absolute";
                cntrlPanel.ID = "cntrlpnl";

                gisImage = new System.Web.UI.WebControls.Image();
                gisImage.ID = "gisImage";
                gisImage.AlternateText = "Map generated by Easy GIS .NET. [www.easygisdotnet.com]";
                gisImage.Style[HtmlTextWriterStyle.Position] = "relative";
                gisImage.Style[HtmlTextWriterStyle.Left] = "0px";
                gisImage.Style[HtmlTextWriterStyle.Top] = "0px";
                gisImage.Style[HtmlTextWriterStyle.Visibility] = "hidden";
                gisImage.Width = this.Width;
                gisImage.Height = h;// this.Height;

                cntrlPanel.Controls.Add(gisImage);
                Panel p = new Panel();
                p.ID = "gisep";
                p.Width = this.Width;
                p.Height = h;// this.Height;
                p.Style[HtmlTextWriterStyle.Position] = "absolute";
                p.Style[HtmlTextWriterStyle.Left] = "0px";
                p.Style[HtmlTextWriterStyle.Top] = "0px";

                //p.BackImageUrl = Page.ClientScript.GetWebResourceUrl(this.GetType(), "egis.web.controls.ajax-loader3.gif");
                p.Style["background-repeat"] = "no-repeat";
                p.Style["background-position"] = "center";


                eventPanel = p;
                cntrlPanel.Controls.Add(p);
                this.Controls.Add(cntrlPanel);


                //hfX = new HiddenField();
                hfX.ID = "hfx";
                this.Controls.Add(hfX);

                //hfY = new HiddenField();
                hfY.ID = "hfy";
                this.Controls.Add(hfY);

                //hfZoom = new HiddenField();
                hfZoom.ID = "hfzoom";
                this.Controls.Add(hfZoom);

                hfcoc = new HiddenField();
                hfcoc.ID = "hfcoc";
                this.Controls.Add(hfcoc);

                hftooltipUrl = new HiddenField();
                hftooltipUrl.ID = "hftooltipurl";
                hftooltipUrl.Value = Page.ClientScript.GetWebResourceUrl(this.GetType(), "EGIS.Web.Controls.tooltip.png");
                this.Controls.Add(hftooltipUrl);                
            }
            base.CreateChildControls();
        }

        /// <summary>
        /// returns the Client id of the internal  GIS Image. This is used by controls only
        /// </summary>
        public string GisImageClientId
        {
            get
            {
                //return (gisImage == null?"":gisImage.ClientID);

                return (this.eventPanel == null ? this.ClientID + "_gisep" : eventPanel.ClientID);
            }
        }

        /// <summary>
        /// returns the name of the client javascript resource used to control the map.
        /// </summary>
        /// <remarks>
        /// <para>this method implements the ClientJSResouceName method of ISFMap interface</para>
        /// </remarks>
        public string ClientJSResouceName
        {
            get
            {
                return "EGIS.Web.Controls.egis_script2.js";
            }
        }

        /// <summary>
        /// returns the client id of the map
        /// </summary>
        /// <remarks>
        /// <para>this method implements the ClientJSResouceName method of ISFMap interface</para>
        /// </remarks>
        public string ControlId
        {
            get
            {
                return this.ID;
            }
        }

        private void CheckHiddenFieldsSet()
        {
            if (hfX != null && hfY != null)
            {
                //if CentrePoint not set then set to centre of map
                if (string.IsNullOrEmpty(hfX.Value) || string.IsNullOrEmpty(hfY.Value))
                {
                    RectangleF r = Extent;
                    if (Extent != RectangleF.Empty)
                    {
                        PointF pt = new PointF(r.Left + r.Width / 2, r.Top + r.Height / 2);
                        hfX.Value = pt.X.ToString(System.Globalization.CultureInfo.InvariantCulture);
                        hfY.Value = pt.Y.ToString(System.Globalization.CultureInfo.InvariantCulture);
                    }
                }                
            }
            if (this.hfZoom != null)
            {
                if (string.IsNullOrEmpty(hfZoom.Value))
                {
                    //if zoom not set then set to fit entire map
                    RectangleF r = Extent;
                    if (Extent != RectangleF.Empty)
                    {
                        hfZoom.Value = "" + ((int)this.Width.Value) / r.Width;
                    }                    
                }                
            }                
        }                

        /// <summary>
        /// overrides OnPreRender in CompositeControl
        /// </summary>
        /// <param name="e"></param>
        protected override void OnPreRender(EventArgs e)
        {
            if (gisImage != null) gisImage.ImageUrl = this.ImageSource;
            if (this.eventPanel != null)
            {
                eventPanel.BackColor = Color.Black;
                eventPanel.BackImageUrl = Page.ClientScript.GetWebResourceUrl(this.GetType(), "EGIS.Web.Controls.ajax-loader2.gif");
            }
            this.CheckHiddenFieldsSet();
            if (hfcoc != null) hfcoc.Value = CacheOnClient.ToString();
            addMapJSHandlers();            
            //Page.Session[DCRSSessionKey] = customRenderSettingsList;

            string pn = Page.ResolveUrl(ProjectName);

            Page.Application[pn + "_CacheOnServer"] = CacheOnServer;
            Page.Application[pn + "_ServerCacheDirectory"] = this.ServerCacheDirectoryUrl;


            base.OnPreRender(e);
        }

        private void addMapJSHandlers()
        {
            //string yahooScript = "EGIS.Web.Controls.yahoo-min.js";
            //string eventScript = "EGIS.Web.Controls.event-min.js";
            string egisScriipt = ClientJSResouceName;
            ClientScriptManager csm = this.Page.ClientScript;
            //csm.RegisterClientScriptResource(this.GetType(), yahooScript);
            //csm.RegisterClientScriptResource(this.GetType(), eventScript);
            csm.RegisterClientScriptResource(this.GetType(), egisScriipt);    
        
            string mapId = Page.ResolveUrl(ProjectName);
            string onloadStr = "mapLoad('" + HttpHandlerName + "' , '" + CenterXFieldClientId + "','" + CenterYFieldClientId + "','" + ZoomFieldClientId + "',this,'" + mapId +
                "','" + this.eventPanel.ClientID + "','" + this.DCRSSessionKey + "','" + this.CacheOnClientFieldClientId + "');";
            
            gisImage.Attributes["onload"] = onloadStr;
        }

        #region methods to create the map and load project

        /// <summary>
        /// Reads the EGIS shapefile project and creates the map
        /// </summary>
        protected void CreateMap()
        {            
            string path = ProjectName;
            if (!string.IsNullOrEmpty(path))
            {
                //List<EGIS.ShapeFileLib.ShapeFile> sfList = this.Page.Application[Page.ResolveUrl(path)] as List<EGIS.ShapeFileLib.ShapeFile>;
                //if (sfList == null)
                MapProject mapProject = this.Page.Application[Page.ResolveUrl(path)] as MapProject;
                if(mapProject == null)
                {
                    this.Page.Application[Page.ResolveUrl(path)] = ReadProject(Page.MapPath(path), this);                     
                }                
            }
            
        }

        internal static MapProject ReadProject(string absPath, TiledSFMap mapRef)
        {
            string basePath = absPath.Substring(0, absPath.LastIndexOf('\\') + 1);
            XmlDocument doc = new XmlDocument();
            doc.Load(absPath);            
            XmlElement prjElement = (XmlElement)doc.GetElementsByTagName("sfproject").Item(0);
            //string version = prjElement.GetAttribute("version");
            return ReadXml(prjElement, basePath, mapRef);            
        }
      
        internal static MapProject ReadXml(XmlElement projectElement, string rootPath, TiledSFMap mapRef)
        {
            MapProject mapProject = new MapProject();

            XmlNodeList colorList = projectElement.GetElementsByTagName("MapBackColor");
            if (colorList != null && colorList.Count > 0)
            {
                mapProject.BackgroundColor = ColorTranslator.FromHtml(colorList[0].InnerText);
                
            }
            else if (mapRef != null)
            {
                mapProject.BackgroundColor = mapRef.BackColor;
            }

            //clear layers
            List<EGIS.ShapeFileLib.ShapeFile> myShapefiles = new List<EGIS.ShapeFileLib.ShapeFile>();

            XmlNodeList layerNodeList = projectElement.GetElementsByTagName("layers");
            XmlNodeList sfList = ((XmlElement)layerNodeList[0]).GetElementsByTagName("shapefile");

            if (sfList != null && sfList.Count > 0)
            {

                for (int n = 0; n < sfList.Count; n++)
                {
                    EGIS.ShapeFileLib.ShapeFile sf = new EGIS.ShapeFileLib.ShapeFile();
                    XmlElement elem = sfList[n] as XmlElement;
                    elem.GetElementsByTagName("path")[0].InnerText = rootPath + elem.GetElementsByTagName("path")[0].InnerText;
                    sf.ReadXml(elem);
                    myShapefiles.Add(sf);
                }                
            }

            //EGIS.ShapeFileLib.ShapeFile.UseMercatorProjection = false;
            mapProject.Layers = myShapefiles;
            return mapProject;
        }

        #endregion


        #region methods to access map layers

        private List<EGIS.ShapeFileLib.ShapeFile> Layers
        {
            get
            {
                MapProject mapProject = this.Page.Application[Page.ResolveUrl(ProjectName)] as MapProject;
                if (mapProject == null) return null;
                return mapProject.Layers;
            }
        }

        /// <summary>
        /// returns the number of layers in the TiledSFMap Control
        /// </summary>
        [Browsable(false)]
        public int LayerCount
        {
            get
            {
                if (this.DesignMode)
                {
                    return 0;
                }
                else
                {
                    List<EGIS.ShapeFileLib.ShapeFile> layers = Layers;
                    return (layers == null ? 0 : layers.Count);
                }
            }            
        }

        /// <summary>
        /// Method to get a ShapeFile layer at the specified index
        /// </summary>
        /// <param name="index">zero-based index of layer to return</param>
        /// <returns> The requested ShapeFile layer</returns>
        public EGIS.ShapeFileLib.ShapeFile GetLayer(int index)
        {
            return Layers[index];
        }

        #endregion

        #region Dynamic Custom Render methods

        private List<SessionCustomRenderSettingsEntry> customRenderSettingsList;

        /// <summary>
        /// Applies custom render settings to the specified layer
        /// </summary>
        /// <remarks>
        /// <para>
        /// The ICustomRenderSettings object is stored in the session settings, meaning that the custom render settings are 
        /// applied per user's session.
        /// </para>
        /// <para>If Custom Render Settings are used then it is usualy neccessary to set CacheOnClient to False
        /// </para>
        /// 
        /// </remarks>
        /// <param name="layerIndex">The zero-based index of the layer to apply the custom render settings</param>
        /// <param name="settings">An ICustomRenderSettings object to set on the specified shapefile</param>        
        /// <seealso cref="EGIS.ShapeFileLib.ICustomRenderSettings"/>
        /// <seealso cref="EGIS.Web.Controls.QuantileCustomRenderSettings"/>
        /// <seealso cref="CacheOnClient"/>
        public void SetCustomRenderSettings(int layerIndex, EGIS.ShapeFileLib.ICustomRenderSettings settings)
        {
            if (customRenderSettingsList == null)
            {
                customRenderSettingsList = new List<SessionCustomRenderSettingsEntry>();
            }
            customRenderSettingsList.Add(new SessionCustomRenderSettingsEntry(layerIndex, settings));
        }

        /// <summary>
        /// Clears any ICustomRenderSettings previously set on the layers of the map
        /// </summary>
        public void ClearCustomRenderSettings()
        {
            customRenderSettingsList = null;

        }

        #endregion

        [Browsable(false)]
        internal string DCRSSessionKey
        {
            get
            {
                return this.ClientID + "_dcrs";
            }
        }


        internal static RectangleD LayerExtent(List<EGIS.ShapeFileLib.ShapeFile> layers)
        {
            if (layers == null || layers.Count == 0)
            {
                return RectangleD.Empty;
            }
            else
            {
                RectangleD r = layers[0].Extent;
                foreach (EGIS.ShapeFileLib.ShapeFile sf in layers)
                {
                    r = RectangleF.Union(r, sf.Extent);
                }
                return r;
            }
        }


        #region Render Methods

        /// <summary>
        /// Renders the control to the specified HTML writer.
        /// </summary>
        /// <param name="writer">The <see cref="T:System.Web.UI.HtmlTextWriter"></see> object that receives the control content.</param>
        protected override void Render(HtmlTextWriter writer)
        {
            if (this.DesignMode)
            {                
                
                this.RenderDesignTime(writer);                
            }
           
            base.Render(writer);

            if(!DesignMode) RenderJS(writer);
            
       }

        private void RenderJS(HtmlTextWriter writer)
        {
            //add init function            
            //string js = "function initMap(){setupMap('" + eventPanel.ClientID + 
            //    "'," + this.MinZoomLevel + "," + MaxZoomLevel + ");";

            string test = string.Format("'{0}'", "test");
            string js = string.Format("function initMap(){{initMapping('{0}','{1}','{2}','{3}','{4}','{5}','{6}','{7}',{8},{9});",
                new object[] { HttpHandlerName, CenterXFieldClientId, CenterYFieldClientId, ZoomFieldClientId ,
                                Page.ResolveUrl(ProjectName), this.eventPanel.ClientID,this.DCRSSessionKey, CacheOnClientFieldClientId,
                                MinZoomLevel.ToString(), MaxZoomLevel.ToString()});
            //+eventPanel.ClientID +
            //        "'," + this.MinZoomLevel + "," + MaxZoomLevel + ");";


            //function initMapping(handlerUrl, hfxid, hfyid, hfzid, mapid, epid, dcrs, coc, minz, maxz)

            //string onloadStr = "mapLoad('" + HttpHandlerName + 
            //    "' , '" + CenterXFieldClientId + "','" + 
            //    CenterYFieldClientId + "','" + ZoomFieldClientId 
            //    + "',this,'" + mapId +
            //    "','" + this.eventPanel.ClientID + 
            //    "','" + this.DCRSSessionKey + "','" + this.CacheOnClientFieldClientId + "');";
            

            string clientZoomChanged = this.OnClientZoomChanged;            
            string zoomFunction = "null";
            if (clientZoomChanged != null)
            {
                //zoomFunction = "function(type,args,cust){" + clientZoomChanged + "}";                
                zoomFunction = clientZoomChanged;                
            }
            string clientBoundsChanged = this.OnClientBoundsChanged;            
            string boundsFunction = "null";
            if (clientBoundsChanged != null)
            {
                //boundsFunction = "function(type,args,cust){" + clientBoundsChanged + "}";                
                boundsFunction = clientBoundsChanged;
            }

            js += "setupMapEventHandlers('" + eventPanel.ClientID + "'," + zoomFunction + "," + boundsFunction + ");";

            js += "}";

            writer.RenderBeginTag(HtmlTextWriterTag.Script);
            writer.WriteLine(js);
            writer.WriteLine("window.onload = initMap;");
            writer.RenderEndTag();            
            
        }        

        /// <summary>
        /// Registers the Http Handler in configuration.
        /// </summary>
        public virtual void RegisterHandlerInConfiguration()
        {
            // supported only at design time 
            if (!this.DesignMode)
            {
                return;
            }

            // no point to add if http handler name or type is null
            if (string.IsNullOrEmpty(this.HttpHandlerName))
            {
                return;
            }

            // get the Web application configuration
            IWebApplication webApplication = (IWebApplication)this.Site.GetService(typeof(IWebApplication));
            if (null != webApplication)
            {
                global::System.Configuration.Configuration webConfig = webApplication.OpenWebConfiguration(false);
                if (null == webConfig)
                {
                    throw new ConfigurationErrorsException("web.config file not found to register the http handler.");
                }

                // get the <system.web> section
                ConfigurationSectionGroup systemWeb = webConfig.GetSectionGroup("system.web");
                if (null == systemWeb)
                {
                    systemWeb = new ConfigurationSectionGroup();
                    webConfig.SectionGroups.Add("system.web", systemWeb);
                }

                // get the <httpHandlers> section
                HttpHandlersSection httpHandlersSection = (HttpHandlersSection)systemWeb.Sections.Get("httpHandlers");
                if (null == httpHandlersSection)
                {
                    httpHandlersSection = new HttpHandlersSection();
                    systemWeb.Sections.Add("httpHandlers", httpHandlersSection);
                }

                // add the image handler
                httpHandlersSection.Handlers.Add(new HttpHandlerAction(this.HttpHandlerName, this.HttpHandlerType.AssemblyQualifiedName, "*"));

                // save the new web config
                webConfig.Save();
            }
        }


        /// <summary>
        /// Renders the control at design time.
        /// </summary>
        /// <param name="writer">The html writer.</param>
        protected virtual void RenderDesignTime(HtmlTextWriter writer)
        {
            if (null == this.Page.Site)
            {
                writer.Write("Error");
                return;
            }
            
            //set the width and height
            if ((Unit.Empty != this.Width) || (Unit.Empty != this.Height))
            {
                if (Unit.Empty != this.Width)
                {
                    writer.AddAttribute(HtmlTextWriterAttribute.Width, this.Width.ToString());
                    if (this.gisImage != null) gisImage.Width = this.Width;
                    if (this.cntrlPanel != null) cntrlPanel.Width = this.Width;
                }
                if (Unit.Empty != this.Height)
                {
                    writer.AddAttribute(HtmlTextWriterAttribute.Height, this.Height.ToString());
                    if (this.gisImage != null) gisImage.Height = this.Height;
                    if (this.cntrlPanel != null) cntrlPanel.Height = this.Height;
                }
            }
            else
            {
                writer.AddAttribute(HtmlTextWriterAttribute.Width, "150px");
                writer.AddAttribute(HtmlTextWriterAttribute.Height, "100px");
            }

            writer.AddStyleAttribute(HtmlTextWriterStyle.BackgroundImage, Page.ClientScript.GetWebResourceUrl(this.GetType(), "EGIS.Web.Controls.globetsfm.gif"));
            writer.AddStyleAttribute("background-repeat", "no-repeat");
            writer.AddStyleAttribute("background-position", "center center");
            
            RegisterHandlerInConfiguration();



        }

        

        #endregion


        #region Public Properties


        /// <summary>
        /// Gets the rectangular extent of the entire map
        /// </summary>
        /// <remarks>
        /// Extent is the rectangular extent of the ENTIRE map, regardless of the current ZoomLevel or CentrePoint.  
        /// </remarks>
        [Browsable(false)]
        public RectangleF Extent
        {
            get
            {
                if (this.DesignMode)
                {
                    return RectangleF.Empty;
                }
                else
                {
                    return LayerExtent(Layers);
                }
            }
        }


        /// <summary>
        /// Gets the image real src that points to the http handler.
        /// It is used as a key for the server cache so shoud be overriden with caution.
        /// </summary>
        /// <value>The image src.</value>
        [Browsable(false)]
        protected string ImageSource
        {
            get
            {
                string src = this.HttpHandlerName;
                src += "?w=" + (int)this.Width.Value + "&h=" + (int)this.Height.Value;
                EGIS.ShapeFileLib.PointD pt = this.CenterPoint;
                if (pt != PointD.Empty)
                {
                    src += "&x=" + pt.X + "&y=" + pt.Y;
                }
                src += "&zoom=" + this.Zoom;
                src += "&mapid=" + Page.ResolveUrl(ProjectName);

                if (!CacheOnClient) src += "&coc=" + DateTime.Now.ToString("yyyyMMddHHmmss", System.Globalization.CultureInfo.InvariantCulture);

                return src;
            }
        }

        /// <summary>
        /// Gets the type of the http handler used to render the map on the server.
        /// </summary>        
        [Browsable(false)]
        public virtual Type HttpHandlerType
        {
            get { return typeof(TiledSFMapImageProvider); }
        }

        /// <summary>
        /// Gets the name of the http handler used to render the map on the server.
        /// </summary>
        [Browsable(true),
        Category("data"),
        Description("The name of the IHttpHandler used to handle map requests")
        ]
        public string HttpHandlerName
        {
            //get
            //{
            //    return "egismaptiled.axd";
            //}
            get
            {
                if (ViewState["TiledSFMapHandler"] == null) return "egismaptiled.axd";
                return (String)ViewState["TiledSFMapHandler"];
            }
            set
            {                
                ViewState["TiledSFMapHandler"] = value;                                
            }
        }

        /// <summary>
        /// Gets or sets the name of the Easy GIS .NET (.egp) project to load in the map.
        /// </summary>
        [EditorAttribute(typeof(EGIS.Web.Controls.ProjectUrlEditor), typeof(System.Drawing.Design.UITypeEditor)), 
        Bindable(true),
        Category("Data"),
        DefaultValue(""),
        Description("The URL of the Easy GIS .NET Project (.egp) loaded in the map"),
        Localizable(true)
        ]
        public string ProjectName
        {
            get
            {
                if (ViewState["ProjectName"] == null) return string.Empty;
                return (String)ViewState["ProjectName"];
            }
            set
            {                
                ViewState["ProjectName"] = value;                                
            }
        }

        /// <summary>
        /// Gets or sets the center point of the map in mapping coordinates (as used by the shapefiles)
        /// </summary>
        /// <remarks>
        /// Changing the CenterPoint can be used to center the map on a new location without 
        /// changing the map scale
        /// <para>
        /// The ZoomLevel, CenterPoint, Width and Height of the TiledSFMap determine the location and visible area of the rendered map. The map will be rendered 
        /// centered at the CenterPoint and scaled according to the ZoomLevel.
        /// </para>
        /// </remarks>
        /// <seealso cref="Zoom"/>
        [Bindable(true),
        Category("Navigation"),
        DefaultValue(""),
        Description("The Center Point of the Map."),
        Localizable(true)
        ]
        public virtual EGIS.ShapeFileLib.PointD CenterPoint
        {
            get
            {
            
                if(hfX != null && hfY != null)
                {
                    //if CentrePoint not set then set to centre of map
                    if (string.IsNullOrEmpty(hfX.Value) || string.IsNullOrEmpty(hfY.Value))
                    {
                        RectangleF r = Extent;
                        if (Extent != RectangleF.Empty)
                        {
                            EGIS.ShapeFileLib.PointD pt =  new EGIS.ShapeFileLib.PointD(r.Left + r.Width / 2, r.Top + r.Height / 2);
                            hfX.Value = pt.X.ToString(System.Globalization.CultureInfo.InvariantCulture);
                            hfY.Value = pt.Y.ToString(System.Globalization.CultureInfo.InvariantCulture);
                            return pt;
                        }
                    }
                    else
                    {
                        double x, y;
                        if (double.TryParse(hfX.Value, out x) && double.TryParse(hfY.Value, out y))
                        {
                            return new EGIS.ShapeFileLib.PointD(x, y);
                        }
                    }
                }
                return EGIS.ShapeFileLib.PointD.Empty;
            }
            set
            {
                if (hfX != null && hfY != null)
                {
                    hfX.Value = value.X.ToString(System.Globalization.CultureInfo.InvariantCulture);
                    hfY.Value = value.Y.ToString(System.Globalization.CultureInfo.InvariantCulture);
                }
            }
        }

        /// <summary>
        /// Gets or sets the current ZoomLevel of the TiledSFMap
        /// </summary>
        /// <remarks>
        /// Changing the ZoomLevel will zoom into or out of the map. Increasing the ZoomLevel will zoom into the map, while decreasing the 
        /// ZoomLevel will zoom out of the map
        /// <para>
        /// The ZoomLevel, CenterPoint, Width and Height of the TiledSFMap determine the location and visible area of the rendered map. The map will be rendered 
        /// centered at the CenterPoint and scaled according to the ZoomLevel.
        /// </para>
        /// </remarks>
        /// <seealso cref="CenterPoint"/>
        /// <exception cref="System.ArgumentException"> if ZoomLevel less than or equal to zero</exception>        
        [Bindable(true),
        Category("Navigation"),
        DefaultValue("1"),
        Description("Map Zoom Level."),
        Localizable(true)
        ]
        public float Zoom
        {
            get
            {            
                if (this.hfZoom != null)
                {
                    if (string.IsNullOrEmpty(hfZoom.Value))
                    {
                        //if zoom not set then set to fit entire map
                        RectangleF r = Extent;
                        if (Extent != RectangleF.Empty)
                        {
                            hfZoom.Value = "" + ((int)this.Width.Value) / r.Width;
                        }
                        else
                        {
                            //no shapefile, just return 1
                            return 1;
                        }
                    }
                    float z;
                    if (float.TryParse(hfZoom.Value, out z))
                    {
                        return z;
                    }
                    else
                    {
                        return 1;
                    }                    
                }
                return 1;
            }
            set
            {
                if (value < float.Epsilon) throw new ArgumentException("ZoomLevel can not be <= Zero");
                if (hfZoom != null)
                {
                    hfZoom.Value = value.ToString(System.Globalization.CultureInfo.InvariantCulture);
                }

            }
        }

        /// <summary>
        /// Gets or Sets the Minimum Allowed ZoomLevel. This is the minimum ZoomLevel that can be set before 
        /// the map can no longer be zoomed out.
        /// </summary>     
        /// <seealso cref="MaxZoomLevel"/>
        [Bindable(true),
        Category("Navigation"),
        Description("Min Allowable Map Zoom Level."),
        Localizable(true)
        ]
        public float MinZoomLevel
        {
            get
            {
                if (ViewState["MinZoomLevel"] == null)
                {
                    ViewState["MinZoomLevel"] = float.Epsilon;                    
                }
                return (float)ViewState["MinZoomLevel"];
            }
            set
            {
                ViewState["MinZoomLevel"] = value;
            }
        }

        /// <summary>
        /// Gets or Sets the Maximum Allowed ZoomLevel. This is the maximum ZoomLevel that can be set before 
        /// the map can no longer be zoomed in.
        /// </summary>        
        /// <seealso cref="MinZoomLevel"/>
        [Bindable(true),
        Category("Navigation"),
        Description("Max Allowable Map Zoom Level."),
        Localizable(true)
        ]
        public float MaxZoomLevel
        {
            get
            {
                if (ViewState["MaxZoomLevel"] == null)
                {
                    ViewState["MaxZoomLevel"] = float.MaxValue;
                }
                return (float)ViewState["MaxZoomLevel"];
            }
            set
            {
                ViewState["MaxZoomLevel"] = value;
            }
        }

        /// <summary>
        /// Gets or sets the client-side script that executes when the map ZoomLevel changes
        /// </summary>
        /// <remarks>
        /// When the zoom level changes a client-side javascript event will be fired with the the following 3 parameters
        /// <list type="bullet">
        /// <item>
        /// <term>type</term>
        /// <description>The event type (="ZoomChanged"). This can be ignored</description>
        /// </item>
        /// <item>
        /// <term>args</term>
        /// <description>args[0] contains the current zoom level</description>
        /// </item>
        /// <item>
        /// <term>obj</term>
        /// <description>The object generating the event. This can be ignored.</description>
        /// </item>
        /// </list>
        /// 
        /// <example>
        /// <code>        
        /// function MapZoomChanged(type, args, obj)
        ///{        
        ///    var debugpanel = document.getElementById('debugpanel');
        ///    debugpanel.innerHTML = '[' + obj.toString() + ',' + type + ']Current Zoom: ' + args[0] + '&lt;br/&gt;' + debugpanel.innerHTML;        
        ///}
        /// ..
        /// &lt;div id = "debugpanel" ..
        /// </code>
        /// In this example OnClientZoomChanged would be set to "MapZoomChanged"
        /// </example>
        /// </remarks>
        [Bindable(true),
        Category("Navigation"),
        Description("Client-side zoom changed event handler."),
        Localizable(true)
        ]
        public string OnClientZoomChanged
        {
            get
            {
                return (string)ViewState["OnClientZoomChanged"];
            }
            set
            {
                ViewState["OnClientZoomChanged"] = value;
            }
        }

        /// <summary>
        /// Gets or sets the client-side script that executes when the map Bounds are changed
        /// </summary>
        /// <remarks>
        /// When the map bounds change a client-side javascript event will be fired with the the following 3 parameters
        /// <list type="bullet">
        /// <item>
        /// <term>type</term>
        /// <description>The event type (="BoundsChanged"). This can be ignored</description>
        /// </item>
        /// <item>
        /// <term>args</term>
        /// <description>The new map bounds. args[0] contains MinX, args[1] contains MaxY, args[2] contains MaxX, args[3] contains MaxY</description>
        /// </item>
        /// <item>
        /// <term>obj</term>
        /// <description>The object generating the event. This can be ignored.</description>
        /// </item>
        /// </list>
        /// 
        /// <example>
        /// <code>        
        ///function MapBoundsChanged(type, args, obj)
        ///{
        ///   var debugPanel = document.getElementById('debugpanel');            
        ///   debugPanel.innerHTML = '[' + obj.toString() + ',' + type + ']Current Bounds: ' + args[0] + ',' + args[1] +  ',' + args[2] + ',' + args[3] + '<br/>' + debugPanel.innerHTML ;
        ///}
        /// ..
        /// &lt;div id = "debugpanel" ..
        /// </code>
        /// In this example OnClientBoundsChanged would be set to "MapBoundsChanged"
        /// </example>
        /// </remarks>
        [Bindable(true),
        Category("Navigation"),
        Description("Client-side bounds changed event handler."),
        Localizable(true)
        ]
        public string OnClientBoundsChanged
        {
            get
            {
                return (string)ViewState["OnClientBoundsChanged"];
            }
            set
            {
                ViewState["OnClientBoundsChanged"] = value;
            }
        }

        /// <summary>
        /// Gets or sets whether to use client-side caching of any generated map image.
        /// </summary>
        /// <remarks>
        /// <para>By default CacheOnClient will be set to true. This means that cache-specific HTTP headers will be set on generated
        /// map images. This will generally improve map performance as successive map images are cached by client web-browsers, however in
        /// some cases this is not desirable.</para>
        /// <para>If you are using CustomRenderSettings then it may be neccessary to set CacheOnClient ot False</para>        
        /// </remarks>
        [Bindable(true),        
        DefaultValue("True"),
        Category("Caching"),
        Description("Whether to use client-side caching of map images"),
        Localizable(true)
        ]
        public bool CacheOnClient
        {
            get
            {
                if (ViewState["CacheOnClient"] == null)
                {
                    //default to true                    
                    ViewState["CacheOnClient"] = true;
                 }                                 
                return (bool)ViewState["CacheOnClient"];
            }
            set
            {
                ViewState["CacheOnClient"] = value;
            }
        }

        /// <summary>
        /// Gets or sets whether to use server-side caching of any generated map tile image.
        /// </summary>
        /// <remarks>
        /// <para>By default CacheOnServer will be set to false. If this property is set to true then ServerCacheDirectoryUrl
        /// must be set to a directory with appropriate write permissions. If images are cached on server then the processing overhead of
        /// rendering map image requests will not be required on the server, but there must be sufficient space on the server to
        /// store images.</para>
        /// <para>If you are using CustomRenderSettings then it may be neccessary to set CacheOnServer to False</para>        
        /// </remarks>
        [Bindable(true),
        DefaultValue("True"),
        Category("Caching"),
        Description("Whether to use server-side caching of map tile images"),
        Localizable(true)
        ]
        public bool CacheOnServer
        {
            get
            {
                if (ViewState["CacheOnServer"] == null)
                {
                    //default to false                    
                    ViewState["CacheOnServer"] = false;
                }
                return (bool)ViewState["CacheOnServer"];
            }
            set
            {
                ViewState["CacheOnServer"] = value;
            }
        }


        /// <summary>
        /// The Url of the directory where image tiles will be cached on the server if <see cref="TiledSFMap.CacheOnServer"/> is set to true        
        /// </summary>
        /// <remarks>
        /// <para>Note that it may be neccessary to give appropriate write permissions to the ASP user account
        /// on the specified directory </para>
        /// <para>If CacheOnServer is false then this parameter will be ignored
        /// </para>        
        /// </remarks>
        [EditorAttribute(typeof(System.Web.UI.Design.UrlEditor), typeof(System.Drawing.Design.UITypeEditor)),
        Bindable(true),        
        Category("Caching"),
        Description("The Url of the directory where image tiles will be cached on the server"),
        Localizable(true)
        ]
        public string ServerCacheDirectoryUrl
        {
            get
            {
                if (ViewState["ServerCacheDirectoryUrl"] == null)
                {
                    //default to false                    
                    ViewState["ServerCacheDirectoryUrl"] = "";
                }
                return ViewState["ServerCacheDirectoryUrl"] as string;
            }
            set
            {
                ViewState["ServerCacheDirectoryUrl"] = value;
            }
        }



        #endregion

        /// <summary>
        /// Overrides LoadViewState method in WebControl
        /// </summary>
        /// <param name="savedState"></param>
        protected override void LoadViewState(object savedState)
        {
            if (savedState != null)
            {
                base.LoadViewState(savedState);
                if (this.ViewState["CenterPoint"] != null)
                {
                    this.CenterPoint = (PointF)this.ViewState["CenterPoint"];
                }
                if (this.ViewState["Zoom"] != null)
                {
                    this.Zoom = (float)this.ViewState["Zoom"];
                }
                if(!string.IsNullOrEmpty(this.ViewState["ProjectName"] as string))
                {
                    this.ProjectName = ViewState["ProjectName"] as string;
                }
            }
        }

        /// <summary>
        /// Convenience method to render the current map to a bitmap
        /// </summary>
        /// <param name="bitmap"></param>
        /// <param name="context"></param>
        public void RenderMap(System.Drawing.Image bitmap, HttpContext context)
        {            
            Graphics g = Graphics.FromImage(bitmap);
            try
            {
                MapProject mapProject = CreateMapLayers(Page.Application, ProjectName, Page.Server.MapPath(ProjectName));
                if (mapProject == null) throw new System.InvalidOperationException("Map project not found");
                g.Clear(mapProject.BackgroundColor);
                List<EGIS.ShapeFileLib.ShapeFile> layers = mapProject.Layers;

                if (layers != null)
                {                    
                    RenderMap(g, layers, bitmap.Width, bitmap.Height, this.CenterPoint, this.Zoom, null);                    
                }
                                
            }
            finally
            {
                g.Dispose();
            }

        }

        #region static methods

        internal static MapProject CreateMapLayers(HttpApplicationState application, string mapid, string mapPath)
        {
            MapProject mapProject = application[mapid] as MapProject;
            if (mapProject == null)
            {
                lock (EGIS.ShapeFileLib.ShapeFile.Sync)
                {
                    application[mapid] = TiledSFMap.ReadProject(mapPath, null);
                }
               
            }
            return application[mapid] as MapProject;

        }

        internal static void RenderMap(Graphics g, List<EGIS.ShapeFileLib.ShapeFile> layers, int w, int h, PointD centerPt, double zoom, List<SessionCustomRenderSettingsEntry> customRenderSettingsList)
        {
            lock (EGIS.ShapeFileLib.ShapeFile.Sync)
            {                                
                RectangleF r = SFMap.LayerExtent(layers);
                if (zoom <= 0) zoom = w / r.Width;
                if (centerPt.IsEmpty)
                {
                    centerPt = new PointD(r.Left + r.Width / 2, r.Top + r.Height / 2);
                }
                //save the existing ICustomRenderSettings and set the dynamicCustomRenderSettings
                List<SessionCustomRenderSettingsEntry> defaultcustomRenderSettingsList = new List<SessionCustomRenderSettingsEntry>();
                if (customRenderSettingsList != null)
                {
                    for (int n = 0; n < customRenderSettingsList.Count; n++)
                    {
                        int layerIndex = customRenderSettingsList[n].LayerIndex;
                        if (layerIndex < layers.Count)
                        {
                            defaultcustomRenderSettingsList.Add(new SessionCustomRenderSettingsEntry(layerIndex, layers[layerIndex].RenderSettings.CustomRenderSettings));
                            layers[layerIndex].RenderSettings.CustomRenderSettings = customRenderSettingsList[n].CustomRenderSettings;
                        }
                    }
                }
                for (int n = 0; n < layers.Count; n++)
                {
                    EGIS.ShapeFileLib.ShapeFile sf = layers[n];
                    //render the layer - TiledSFMap always uses Mercator Projection
                    sf.Render(g, new Size(w, h), centerPt, zoom, ProjectionType.Mercator);
                }
                //restore any existing ICustomRenderSettings
                if (customRenderSettingsList != null)
                {
                    for (int n = 0; n < defaultcustomRenderSettingsList.Count; n++)
                    {
                        layers[defaultcustomRenderSettingsList[n].LayerIndex].RenderSettings.CustomRenderSettings = defaultcustomRenderSettingsList[n].CustomRenderSettings;
                    }
                }
            
            }
        }
        
        private static byte[] ParseStringByteArray(string s)
        {
            string[] hexChars = s.Split(new char[] { '-' }, StringSplitOptions.RemoveEmptyEntries);
            byte[] result = new byte[hexChars.Length];

            for (int n = 0; n < result.Length; n++)
            {
                result[n] = byte.Parse(hexChars[n], System.Globalization.NumberStyles.HexNumber, System.Globalization.CultureInfo.InvariantCulture);
            }
            return result;
        }

        internal static string GetDomainName(HttpRequest request)
        {

            string domainName = request.Url.Host.ToLower(System.Globalization.CultureInfo.InvariantCulture);
            if (request.Params["Alias"] != null)
            {
                string portAlias = request.QueryString["Alias"];
                if (!string.IsNullOrEmpty(portAlias))
                {
                    domainName = portAlias.ToLower(System.Globalization.CultureInfo.InvariantCulture) + "." + domainName;
                }
            }
            if (!string.IsNullOrEmpty(domainName))
            {
                if (domainName.IndexOf("www.") == 0)
                {
                    domainName = domainName.Remove(0, 4);
                }
            }
            return domainName;
        }

        
        #endregion

    }

    //internal class SessionCustomRenderSettingsEntry
    //{
    //    public int LayerIndex;
    //    public EGIS.ShapeFileLib.ICustomRenderSettings CustomRenderSettings;

    //    public SessionCustomRenderSettingsEntry(int layerIndex, EGIS.ShapeFileLib.ICustomRenderSettings renderSettings)
    //    {
    //        LayerIndex = layerIndex;
    //        CustomRenderSettings = renderSettings;
    //    }
    //}

    //internal class MapProject
    //{
    //    internal List<EGIS.ShapeFileLib.ShapeFile> Layers;
    //    internal Color BackgroundColor;
    //}

    internal sealed class TiledSFMapImageProvider : IHttpHandler//, System.Web.SessionState.IReadOnlySessionState
    {

        #region IHttpHandler Members

        public bool IsReusable
        {
            get { return true; }
        }

        
        private static MapProject CreateMapLayers(HttpContext context, string mapid)
        {            
            MapProject m = SFMap.CreateMapLayers(context.Application, mapid, context.Server.MapPath(mapid));            
            return m;
        }        

        public void ProcessRequest(HttpContext context)
        {
            ProcessRequestCore(context);            
        }


        private static string LocateShape(PointD pt, List<EGIS.ShapeFileLib.ShapeFile> layers, int zoomLevel, List<SessionCustomRenderSettingsEntry> customRenderSettingsList)
        {
            //changed V3.3 - coords now sent in lat/long
            //convert pt to lat long from merc
            //pt = ShapeFile.MercatorToLL(pt);
            //changed V3.3 - zoom now sent as zoom level
            double zoom = TileUtil.ZoomLevelToScale(zoomLevel);
                        
            float delta = 8f / (float)zoom;
            PointF ptf = new PointF((float)pt.X, (float)pt.Y);
            //save the existing ICustomRenderSettings and set the dynamicCustomRenderSettings
            List<SessionCustomRenderSettingsEntry> defaultcustomRenderSettingsList = new List<SessionCustomRenderSettingsEntry>();
            if (customRenderSettingsList != null)
            {
                for (int n = 0; n < customRenderSettingsList.Count; n++)
                {
                    int layerIndex = customRenderSettingsList[n].LayerIndex;
                    if (layerIndex < layers.Count)
                    {
                        defaultcustomRenderSettingsList.Add(new SessionCustomRenderSettingsEntry(layerIndex, layers[layerIndex].RenderSettings.CustomRenderSettings));
                        layers[layerIndex].RenderSettings.CustomRenderSettings = customRenderSettingsList[n].CustomRenderSettings;
                    }
                }
            }
            
            try
            {

                for (int l = layers.Count - 1; l >= 0; l--)
                {
                    bool useToolTip = (layers[l].RenderSettings != null && layers[l].RenderSettings.UseToolTip);
                    bool useCustomToolTip = (useToolTip && layers[l].RenderSettings.CustomRenderSettings != null && layers[l].RenderSettings.CustomRenderSettings.UseCustomTooltips);                    
                    if (layers[l].Extent.Contains(ptf) && layers[l].IsVisibleAtZoomLevel((float)zoom) && useToolTip)
                    {
                        int selectedIndex = layers[l].GetShapeIndexContainingPoint(ptf, delta);
                        if (selectedIndex >= 0)
                        {
                            if (useCustomToolTip)
                            {
                                return layers[l].RenderSettings.CustomRenderSettings.GetRecordToolTip(selectedIndex);
                            }
                            else
                            {

                                string s = "record : " + selectedIndex.ToString(System.Globalization.CultureInfo.InvariantCulture);
                                if (layers[l].RenderSettings.ToolTipFieldIndex >= 0)
                                {
                                    string temp = layers[l].RenderSettings.DbfReader.GetField(selectedIndex, layers[l].RenderSettings.ToolTipFieldIndex).Trim();
                                    if (temp.Length > 0)
                                    {
                                        s += "<br/>" + temp;
                                    }
                                }
                                return s;
                            }
                        }
                    }
                }
            }
            finally
            {
                //restore any existing ICustomRenderSettings
                if (customRenderSettingsList != null)
                {
                    for (int n = 0; n < defaultcustomRenderSettingsList.Count; n++)
                    {
                        layers[defaultcustomRenderSettingsList[n].LayerIndex].RenderSettings.CustomRenderSettings = defaultcustomRenderSettingsList[n].CustomRenderSettings;
                    }
                }
            }
            return null;
        }


        private static void ProcessGetShapeRequest(HttpContext context)
        {
            double x, y;
            PointD centerPoint = PointD.Empty;
            int zoomLevel = -1;
            string dcrsSessionKey;
            string tooltipText = "";

            if (!double.TryParse(context.Request["x"], out x))
            {
                throw new ArgumentException("invalid x point");

            }
            if (!double.TryParse(context.Request["y"], out y))
            {
                throw new ArgumentException("invalid y point");
            }
            centerPoint = new PointD(x, y);

            //V3.3 zoom now sent as zoom level
            if (!int.TryParse(context.Request["zoom"], out zoomLevel))
            {
                throw new ArgumentException("zoom");
            }

            string mapid = context.Request["mapid"];
            if (string.IsNullOrEmpty(mapid))
            {
                throw new ArgumentException("incorrect parameters - mapid is missing");
            }

            dcrsSessionKey = context.Request["dcrs"];
            
            MapProject mapProject = CreateMapLayers(context, mapid);
            if(mapProject != null && mapProject.Layers != null)
            {
                lock (EGIS.ShapeFileLib.ShapeFile.Sync)
                {
                    //if (!string.IsNullOrEmpty(dcrsSessionKey))
                    //{
                    //    tooltipText = LocateShape(centerPoint, mapProject.Layers, zoom, context.Session[dcrsSessionKey] as List<SessionCustomRenderSettingsEntry>);
                    //}
                    //else
                    {
                        tooltipText = LocateShape(centerPoint, mapProject.Layers, zoomLevel, null);
                    }

                }
            }

            context.Response.ContentType = "text/plain";
            context.Response.Cache.SetCacheability(HttpCacheability.Public);
            context.Response.Cache.SetExpires(DateTime.Now.AddDays(7));
                    
            if (!string.IsNullOrEmpty(tooltipText))
            {
                context.Response.Write("true\n");
                context.Response.Write(string.Format(System.Globalization.CultureInfo.InvariantCulture,"{0},{1}\n", x,y));

                context.Response.Write(tooltipText);
            }
            else
            {
                context.Response.Write("false\n");
            }
            context.Response.Flush();
            //context.Response.End();
        }

        private bool CacheOnServer(HttpContext context, string projectName, ref string location)
        {
            object cache = context.Application[projectName+"_CacheOnServer"];
            if (cache == null || !((bool)cache)) return false;
            location = context.Application[projectName + "_ServerCacheDirectory"] as string;
            if (location == null) return false;
            location = context.Server.MapPath(location);
            return System.IO.Directory.Exists(location);

        }

        
        private static string CreateCachePath(string cacheDirectory, int tileX, int tileY, int zoom)
        {
            string file = string.Format("{0}_{1}_{2}.png", new object[] { tileX, tileY, zoom });
            return System.IO.Path.Combine(cacheDirectory, file);
        }

        private void ProcessRequestCore(HttpContext context)
        {
            DateTime dts = DateTime.Now;
            if (context.Request.Params["getshape"] != null)
            {
                ProcessGetShapeRequest(context);
                return;
            }
            int w = 256*3;
            int h = 256*3;
            int tileX, tileY, zoomLevel;
            PointD centerPoint = PointD.Empty;
            double zoom = -1;
            string dcrsSessionKey;


            string mapid = context.Request["mapid"];
            if (string.IsNullOrEmpty(mapid))
            {
                throw new ArgumentException("incorrect parameters - mapid is missing");
            }

            string cachePath = "";
            string cacheDirectory = "";
            bool useCache = CacheOnServer(context, mapid, ref cacheDirectory);
            cacheDirectory = context.Server.MapPath("cache");
            useCache = true;
            if(int.TryParse(context.Request["tx"], out tileX))
            {
                if (int.TryParse(context.Request["ty"], out tileY))
                {
                    if (int.TryParse(context.Request["zoom"], out zoomLevel))
                    {
                        centerPoint = TileUtil.GetMercatorCenterPointFromTile(tileX, tileY, zoomLevel);
                        zoom = TileUtil.ZoomLevelToScale(zoomLevel);
                        cachePath = CreateCachePath(cacheDirectory, tileX, tileY, zoomLevel);
                    }
                }           
     
            }
            
            if(cachePath == "") useCache = false;
            

            dcrsSessionKey = context.Request["dcrs"];

            context.Response.ContentType = "image/x-png";

            
            if(useCache && System.IO.File.Exists(cachePath))
            {
                context.Response.Cache.SetCacheability(HttpCacheability.Public);
                context.Response.Cache.SetExpires(DateTime.Now.AddDays(7));               
                context.Response.WriteFile(cachePath);
                context.Response.Flush();
                return;
            }

            Bitmap bm = new Bitmap(256, 256, PixelFormat.Format24bppRgb);
            Bitmap bm2 = new Bitmap(w, h, PixelFormat.Format24bppRgb);
            try
            {
                Graphics g = Graphics.FromImage(bm);
                Graphics g2 = Graphics.FromImage(bm2);
                try
                {
                    MapProject mapProject = CreateMapLayers(context, mapid);
                    if (mapProject == null || mapProject.Layers == null) throw new InvalidOperationException("No Map Project");
                    
                    g2.Clear(mapProject.BackgroundColor);
                                                                
                    TiledSFMap.RenderMap(g2, mapProject.Layers, w, h, centerPoint, zoom, null);
                    
                    g.DrawImage(bm2, Rectangle.FromLTRB(0, 0, 256, 256), Rectangle.FromLTRB(256, 256, 512, 512), GraphicsUnit.Pixel);
                                        
                }
                finally
                {
                    g.Dispose();
                    g2.Dispose();
                }
                using (MemoryStream ms = new MemoryStream())
                {
                    if (useCache)
                    {
                        try
                        {
                            bm.Save(cachePath, ImageFormat.Png);
                        }
                        catch { }
                    }
                    bm.Save(ms, ImageFormat.Png);
                    
                    context.Response.Cache.SetCacheability(HttpCacheability.Public);
                    context.Response.Cache.SetExpires(DateTime.Now.AddDays(7));
                    ms.WriteTo(context.Response.OutputStream);
                }                
            }
            finally
            {
                bm.Dispose();
                bm2.Dispose();
            }
            context.Response.Flush();
            //context.Response.End();
        }

        #endregion
    }   
    
}






