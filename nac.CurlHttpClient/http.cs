﻿using System;
using System.Collections.Generic;
using System.Linq;
using nac.CurlHttpClient.model;
using nac.CurlThin.Enums;
using curl = nac.CurlThin.CurlNative.Easy;
using CurlHandleType = nac.CurlThin.SafeHandles.SafeEasyHandle;

namespace nac.CurlHttpClient
{
    public class http
    {
        private model.HttpSetup options;

        public http(model.HttpSetup __options=null)
        {
            if (__options == null)
            {
                this.options = new HttpSetup();
            }
            else
            {
                this.options = __options;
            }

            var globalCurlInitResult = nac.CurlThin.CurlNative.Init(); // this gets called once, but we init curl easy every request
        }


        public http useProxy(string host, int port)
        {
            this.options.useProxy = true;
            this.options.proxyHost = host;
            this.options.proxyPort = port;
            return this;
        }


        private void curlSetupAuthentication(CurlHandleType curlHandle)
        {
            if (this.options.useKerberosAuthentication)
            {
                // kerberos auth
                curl.SetOpt(curlHandle, CURLoption.HTTPAUTH, CURLAUTH.GSSNEGOTIATE);
                curl.SetOpt(curlHandle, CURLoption.USERPWD, ":");
            }
            else if( !string.IsNullOrWhiteSpace(this.options.user))
            {
                // basic auth
                curl.SetOpt(curlHandle, CURLoption.HTTPAUTH, CURLAUTH.BASIC);
                curl.SetOpt(curlHandle, CURLoption.USERPWD, this.options.getUserPasswordCurlOptValue());
            }
        }

        private void curlSetupCookies(CurlHandleType curlHandle)
        {
            // cookies, got a bunch of info from this place on how to do cookies: http://stackoverflow.com/questions/13020404/keeping-session-alive-with-curl-and-php
            
            //string cookieFilePath = "/tmp/cookie.txt";
            string cookieFilePath = "";
            curl.SetOpt(curlHandle, CURLoption.COOKIEJAR, cookieFilePath);
            curl.SetOpt(curlHandle, CURLoption.COOKIEFILE, cookieFilePath);
            curl.SetOpt(curlHandle, CURLoption.COOKIESESSION, 1);
        }


        private void curlSetupSSLVerification(CurlHandleType curlHandle)
        {
            // for now don't do any ssl verification, later we could make this an option or something
            curl.SetOpt(curlHandle, CURLoption.SSL_VERIFYHOST, 0);
            curl.SetOpt(curlHandle, CURLoption.SSL_VERIFYPEER, 0);
        }


        private CurlHandleType curlSetup()
        {
            var curlHandle = curl.Init();

            curl.SetOpt(curlHandle, CURLoption.USERAGENT,
                "Mozilla/5.0 (Windows; U; Windows NT 5.1; en-US; rv:1.8.1.1) Gecko/20061204 Firefox/2.0.0.1");
            curl.SetOpt(curlHandle, CURLoption.FOLLOWLOCATION, 1);

            if (this.options.useProxy)
            {
                curl.SetOpt(curlHandle, CURLoption.PROXY, this.options.getProxyCurlOptValue());
            }
            
            this.curlSetupAuthentication(curlHandle);
            //this.curlSetupCookies(curlHandle);
            this.curlSetupSSLVerification(curlHandle);

            return curlHandle;
        }
        
        private bool isAbsoluteUrl(string url)
        {
            url = url?.Trim() ?? "";

            return System.Text.RegularExpressions.Regex.IsMatch(url, "^https?://");
        }

        private nac.CurlThin.SafeHandles.SafeSlistHandle curlSetHeader(CurlHandleType curlHandle,
                                                                    Dictionary<string,string> headers)
        {
            // followed documentation here: https://curl.se/libcurl/c/CURLOPT_HTTPHEADER.html
            if (headers?.Any() == true)
            {
                nac.CurlThin.SafeHandles.SafeSlistHandle list = null;

                foreach (var pair in headers)
                {
                    list = nac.CurlThin.CurlNative.Slist.Append(list, $"{pair.Key}: {pair.Value}");
                }

                curl.SetOpt(curlHandle, CURLoption.HTTPHEADER, list.DangerousGetHandle());

                return list; // list will need to be freed
            }

            return null;
        }


        private model.CurlExecResult execCurl(CurlHandleType curlHandle, string url, Dictionary<string,string> headers=null)
        {
            var result = new model.CurlExecResult();
            if (!this.isAbsoluteUrl(url))
            {
                url = this.options.appendToBaseAddress(url);
            }
            
            // we know the final URL here
            result.RequestUrl = url;
            
            var headerListHandle = this.curlSetHeader(curlHandle, headers);
            curl.SetOpt(curlHandle, CURLoption.URL, url);
            
            result.ResponseStream = new System.IO.MemoryStream();
            curl.SetOpt(curlHandle, CURLoption.WRITEFUNCTION, (data, size, nmemb, user) =>
            {
                var length = (int) size * (int) nmemb;
                var buffer = new byte[length];
                System.Runtime.InteropServices.Marshal.Copy(data, buffer, 0, length);
                result.ResponseStream.Write(buffer, 0, length);
                return (UIntPtr) length;
            });

            result.ResponseCode = curl.Perform(curlHandle);
            
            // free up some stuff
            if (headerListHandle != null)
            {
                nac.CurlThin.CurlNative.Slist.FreeAll(headerListHandle);
            }
            
            // give back result
            return result;
        }
        
        
        
        
        
        
    }
}
