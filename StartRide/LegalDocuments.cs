using System;
using System.Collections.Generic;

namespace StartRide.Core
{

    public sealed class LegalDocument
    {
        public LegalDocument(string id, string titleKey, string descriptionKey, string resourceName, bool showOnFirstRun)
        {
            Id = id;
            TitleKey = titleKey;
            DescriptionKey = descriptionKey;
            ResourceName = resourceName;
            ShowOnFirstRun = showOnFirstRun;
        }

        public string Id { get; }

        public string TitleKey { get; }

        public string DescriptionKey { get; }

        public string ResourceName { get; }

        public bool ShowOnFirstRun { get; }
    }

    public static class LegalDocuments
    {
        private const string ResourcePrefix = "StartRide.App.Resources.Legal.";

        public static IReadOnlyList<LegalDocument> All { get; } = new[]
        {
            new LegalDocument(
                "user-agreement",
                "Legal_Doc_UserAgreement_Title",
                "Legal_Doc_UserAgreement_Description",
                ResourcePrefix + "01-user-agreement.md",
                showOnFirstRun: true),

            new LegalDocument(
                "privacy-policy",
                "Legal_Doc_PrivacyPolicy_Title",
                "Legal_Doc_PrivacyPolicy_Description",
                ResourcePrefix + "02-privacy-policy.md",
                showOnFirstRun: true),

            new LegalDocument(
                "minor-protection",
                "Legal_Doc_MinorProtection_Title",
                "Legal_Doc_MinorProtection_Description",
                ResourcePrefix + "03-minor-protection.md",
                showOnFirstRun: true),

            new LegalDocument(
                "disclaimer",
                "Legal_Doc_Disclaimer_Title",
                "Legal_Doc_Disclaimer_Description",
                ResourcePrefix + "04-disclaimer.md",
                showOnFirstRun: true),

            new LegalDocument(
                "multiplayer-conduct",
                "Legal_Doc_MultiplayerConduct_Title",
                "Legal_Doc_MultiplayerConduct_Description",
                ResourcePrefix + "05-multiplayer-conduct.md",
                showOnFirstRun: true),

            new LegalDocument(
                "third-party-notices",
                "Legal_Doc_ThirdPartyNotices_Title",
                "Legal_Doc_ThirdPartyNotices_Description",
                ResourcePrefix + "06-third-party-notices.md",
                showOnFirstRun: true),

            new LegalDocument(
                "copyright",
                "Legal_Doc_Copyright_Title",
                "Legal_Doc_Copyright_Description",
                ResourcePrefix + "07-open-source-license.md",
                showOnFirstRun: false),

            new LegalDocument(
                "license",
                "Legal_Doc_License_Title",
                "Legal_Doc_License_Description",
                ResourcePrefix + "07-open-source-license.md",
                showOnFirstRun: false),
        };

        public static IReadOnlyList<LegalDocument> FirstRunAgreement { get; } = Filter(showOnFirstRun: true);

        public static LegalDocument? Find(string? id)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                return null;
            }

            foreach (LegalDocument document in All)
            {
                if (string.Equals(document.Id, id, StringComparison.Ordinal))
                {
                    return document;
                }
            }

            return null;
        }

        private static IReadOnlyList<LegalDocument> Filter(bool showOnFirstRun)
        {
            var list = new List<LegalDocument>();
            foreach (LegalDocument document in All)
            {
                if (document.ShowOnFirstRun == showOnFirstRun)
                {
                    list.Add(document);
                }
            }

            return list;
        }
    }
}
