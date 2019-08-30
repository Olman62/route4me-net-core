﻿using Route4MeDB.ApplicationCore.Interfaces;
using Ardalis.GuardClauses;
using System;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Route4MeDB.ApplicationCore.Entities.OrderAggregate
{
    public class Order : BaseEntity, IAggregateRoot
    {
        public Order()
        {
            // required by EF
        }

        public Order(Order orderParameters)
        {

        }

        public Order(string address1, double cachedLat, double cachedLng, string addressAlias = null, int? orderId = null)
        {
            Guard.Against.NullOrEmpty(address1, nameof(address1));
            Guard.Against.Null(cachedLat, nameof(cachedLat));
            Guard.Against.Null(cachedLng, nameof(cachedLng));

            Address1 = address1;
            CachedLat = cachedLat;
            CachedLng = cachedLng;

            if (orderId != null) OrderId = Convert.ToInt32(orderId);
            if (addressAlias != null) AddressAlias = addressAlias;
        }

        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        [Key, Column("order_db_id")]
        public int OrderDbId { get; set; }

        [Column("order_id")]
        public int? OrderId { get; set; }

        [Column("address_1")]
        public string Address1 { get; set; }

        [Column("address_2")]
        public string Address2 { get; set; }

        [Range(-90, 90)]
        [Column("cached_lat")]
        public double CachedLat { get; set; }

        [Range(-180, 180)]
        [Column("cached_lng")]
        public double CachedLng { get; set; }

        [Range(-90, 90)]
        [Column("curbside_lat")]
        public double CurbsideLat { get; set; }

        [Range(-180, 180)]
        [Column("curbside_lng")]
        public double CurbsideLng { get; set; }

        [DisplayFormat(ApplyFormatInEditMode = true, DataFormatString = "{yyyy-MM-dd}")]
        [RegularExpression(@"2(0|1)[0-9]{2}-(0[1-9]|1[012])-(0[1-9]|[12][0-9]|3[01])")]
        [Column("day_scheduled_for_YYMMDD")]
        public string DayScheduledForYyMmDd { get; set; }

        [DisplayFormat(ApplyFormatInEditMode = true, DataFormatString = "{yyyy-MM-dd}")]
        [RegularExpression(@"2(0|1)[0-9]{2}-(0[1-9]|1[012])-(0[1-9]|[12][0-9]|3[01])")]
        [Column("day_added_YYMMDD")]
        public string DayAddedYyMmDd { get; set; }

        [Column("address_alias")]
        public string AddressAlias { get; set; }

        [Column("local_time_window_start")]
        public int? LocalTimeWindowStart { get; set; }

        [Column("local_time_window_end")]
        public int? LocalTimeWindowEnd { get; set; }

        [Column("local_time_window_start_2")]
        public int? LocalTimeWindowStart2 { get; set; }

        [Column("local_time_window_end_2")]
        public int? LocalTimeWindowEnd2 { get; set; }

        [Column("service_time")]
        public int? ServiceTime { get; set; }

        [Column("address_city")]
        public string AddressCity { get; set; }

        [Column("address_state_id")]
        public string AddressStateId { get; set; }

        [Column("address_country_id")]
        public string AddressCountryId { get; set; }

        [RegularExpression("^[0-9]{5}(?:-[0-9]{4})?$")]
        [Column("address_zip")]
        public string AddressZip { get; set; }

        [Column("order_status_id")]
        public int? OrderStatusId { get; set; }

        [Column("member_id")]
        public int? MemberId { get; set; }

        [Column("EXT_FIELD_first_name")]
        public string EXT_FIELD_first_name { get; set; }

        [Column("EXT_FIELD_last_name")]
        public string EXT_FIELD_last_name { get; set; }

        [Column("EXT_FIELD_email")]
        public string EXT_FIELD_email { get; set; }

        [Column("EXT_FIELD_phone")]
        public string EXT_FIELD_phone { get; set; }

        [Column("EXT_FIELD_custom_data")]
        public string ExtFieldCustomData { get; set; }

        [NotMapped]
        public Dictionary<string, string> EXT_FIELD_custom_datas
        {
            get { return ExtFieldCustomData == null ? null : JsonConvert.DeserializeObject<Dictionary<string, string>>(ExtFieldCustomData); }
            set { ExtFieldCustomData = JsonConvert.SerializeObject(value); }
        }

        [Column("local_timezone_string")]
        public string LocalTimezoneString { get; set; }

        [Column("order_icon")]
        public string OrderIcon { get; set; }
    }
}
