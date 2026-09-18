using System.Collections.Generic;

namespace UninstallerPro
{
    public enum PrivacyProfile { Balanced, Maximum }

    // אשף פרטיות: במקום לגרום למשתמש לעבור טוגל-טוגל, מציע שתי "פרופילי"
    // התחלה סבירים. "מאוזן" משאיר דברים בעלי תועלת ממשית ופרטיות נמוכת-סיכון
    // (מיקום, היסטוריית פעילות) ורק מכבה מעקב/פרסום. "מקסימלי" מכבה הכל.
    // המשתמש עדיין רואה בדיוק מה משתנה לפני שהוא מאשר.
    public static class PrivacyWizard
    {
        public static Dictionary<string, bool> GetProfileValues(PrivacyProfile profile)
        {
            var v = new Dictionary<string, bool>
            {
                { "advertising_id", false },
                { "tailored_experiences", false },
                { "diagnostic_data", false },
                { "app_suggestions", false },
                { "activity_history", profile == PrivacyProfile.Balanced },
                { "feedback_requests", profile == PrivacyProfile.Balanced },
                { "location_services", profile == PrivacyProfile.Balanced },
            };
            return v;
        }

        public static void Apply(PrivacyProfile profile)
        {
            foreach (var kv in GetProfileValues(profile))
            {
                PrivacyData.SetToggle(kv.Key, kv.Value);
            }
        }
    }
}
