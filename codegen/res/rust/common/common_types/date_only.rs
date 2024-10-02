/* This file will be copied into the folder for generated code. */

use std::ops::{Deref, DerefMut};
use serde::{ser, de, Deserialize, Serialize, Deserializer, Serializer};
use chrono::{TimeZone, Utc};
use time::{self, format_description::well_known::Rfc3339};

#[derive(Clone, Debug)]
pub struct Date (time::Date);

impl Deref for Date {
    type Target = time::Date;

    fn deref(&self) -> &Self::Target {
        &self.0
    }
}

impl DerefMut for Date {
    fn deref_mut(&mut self) -> &mut Self::Target {
        &mut self.0
    }
}

impl Serialize for Date {
    fn serialize<S>(&self, s: S) -> Result<S::Ok, S::Error> where S: Serializer {
        s.serialize_str(date_to_rfc3339(self).map_err(|m| ser::Error::custom(m))?.as_str())
    }
}

impl<'de> Deserialize<'de> for Date {
    fn deserialize<D>(deserializer: D) -> Result<Date, D::Error> where D: Deserializer<'de> {
        let s: String = String::deserialize(deserializer)?;
        Ok(rfc3339_to_date(&s).map_err(|m| de::Error::custom(m))?)
    }
}

fn date_to_rfc3339(date: &Date) -> Result<String, &'static str> {
    let date_time = Utc.with_ymd_and_hms(date.year(), date.month() as u32, date.day() as u32, 0, 0, 0).unwrap();
    let date_time_string = date_time.to_rfc3339();
    let t_ix = date_time_string.find('T').ok_or("error serializing Date to RFC 3339 format")?;
    Ok((&date_time_string[..t_ix]).to_string())
}

fn rfc3339_to_date(date_str: &str) -> Result<Date, &'static str> {
    Ok(Date(time::Date::parse(format!("{}T00:00:00Z", date_str).as_str(), &Rfc3339).map_err(|_| { "error deserializing Date from RFC 3339 format" })?))
}
