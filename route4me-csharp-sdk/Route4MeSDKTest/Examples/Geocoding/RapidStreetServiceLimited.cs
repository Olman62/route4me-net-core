﻿using Route4MeSDK.QueryTypes;

namespace Route4MeSDK.Examples
{
    public sealed partial class Route4MeExamples
    {
        /// <summary>
        /// Rapid Street Service Limited
        /// </summary>
        public void RapidStreetServiceLimited()
        {
            // Create the manager with the api key
            var route4Me = new Route4MeManager(ActualApiKey);

            var geoParams = new GeocodingParameters()
            {
                Zipcode = "00601",
                Housenumber = "17",
                Offset = 1,
                Limit = 10
            };

            // Run the query
            var result = route4Me.RapidStreetService(geoParams, out string errorString);

            PrintExampleGeocodings(result, GeocodingPrintOption.StreetService, errorString);
        }
    }
}
