﻿using Route4MeSDK.QueryTypes;

namespace Route4MeSDK.Examples
{
    public sealed partial class Route4MeExamples
    {
        /// <summary>
        /// Rapid Street Zipcode All
        /// </summary>
        public void RapidStreetZipcodeAll()
        {
            // Create the manager with the api key
            var route4Me = new Route4MeManager(ActualApiKey);

            var geoParams = new GeocodingParameters()
            {
                Zipcode = "00601"
            };

            // Run the query
            var result = route4Me.RapidStreetZipcode(geoParams, out string errorString);

            PrintExampleGeocodings(result, GeocodingPrintOption.StreetZipCode, errorString);
        }
    }
}
