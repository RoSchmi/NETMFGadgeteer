using System;
using Microsoft.SPOT;
using System.Net;

namespace RoSchmi.Net
{
    public struct BodyHttpResponse
    {
        public string Body { get; set; }
        public HttpStatusCode StatusCode { get; set; }
    }
}


