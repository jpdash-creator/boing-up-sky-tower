using System;
using System.Collections.Generic;
using FluffyClimbers.Gameplay;
using NUnit.Framework;

namespace FluffyClimbers.Tests
{
    /// <summary>
    /// The Settings legal links (owner 2026-11-09, docs/47 and Planning/LEGAL-HOSTING.md): defaults, per-field fallback when legal_config.json is wrong,
    /// https-only opening through the replaceable opener, and the shipped Resources/Services/legal_config.json itself.
    /// </summary>
    public class LegalLinksTests
    {
        [SetUp]
        public void SetUp() { LegalLinks.ResetForTests(); }

        [TearDown]
        public void TearDown() { LegalLinks.ResetForTests(); }

        [Test]
        public void DefaultsAreHttpsPagesOfOneLegalSiteAndAMailbox()
        {
            Assert.IsTrue(LegalLinks.IsWebUrl(LegalLinks.DefaultPrivacyUrl), LegalLinks.DefaultPrivacyUrl);
            Assert.IsTrue(LegalLinks.IsWebUrl(LegalLinks.DefaultTermsUrl), LegalLinks.DefaultTermsUrl);
            Assert.AreNotEqual(LegalLinks.DefaultPrivacyUrl, LegalLinks.DefaultTermsUrl);
            Assert.AreEqual(new Uri(LegalLinks.DefaultPrivacyUrl).Host, new Uri(LegalLinks.DefaultTermsUrl).Host, "both pages on the same site");
            Assert.IsTrue(LegalLinks.DefaultPrivacyUrl.EndsWith("/policy.html"), "the policy page is docs/policy.html (lowercase, GitHub Pages is case sensitive)");
            Assert.IsTrue(LegalLinks.DefaultTermsUrl.EndsWith("/terms.html"), "the terms page is docs/terms.html");
            Assert.IsTrue(LegalLinks.IsEmail(LegalLinks.DefaultSupportEmail));
        }

        [Test]
        public void ParseReadsAllThreeValues()
        {
            LegalLinks.Config c = LegalLinks.Parse("{\"privacyUrl\":\"https://example.com/p\",\"termsUrl\":\"https://example.com/t\",\"supportEmail\":\"help@example.com\"}");
            Assert.AreEqual("https://example.com/p", c.PrivacyUrl);
            Assert.AreEqual("https://example.com/t", c.TermsUrl);
            Assert.AreEqual("help@example.com", c.SupportEmail);
        }

        [Test]
        public void ParseFallsBackPerFieldForMissingEmptyOrInsecureValues()
        {
            // http is not accepted, an empty terms url falls back, a bad mailbox falls back; the other fields are untouched
            LegalLinks.Config c = LegalLinks.Parse("{\"privacyUrl\":\"http://example.com/p\",\"termsUrl\":\"\",\"supportEmail\":\"nomail\"}");
            Assert.AreEqual(LegalLinks.DefaultPrivacyUrl, c.PrivacyUrl, "http privacy url rejected");
            Assert.AreEqual(LegalLinks.DefaultTermsUrl, c.TermsUrl, "empty terms url rejected");
            Assert.AreEqual(LegalLinks.DefaultSupportEmail, c.SupportEmail, "bad mailbox rejected");

            LegalLinks.Config partial = LegalLinks.Parse("{\"termsUrl\":\"https://example.com/terms \"}");
            Assert.AreEqual(LegalLinks.DefaultPrivacyUrl, partial.PrivacyUrl, "missing privacy url falls back");
            Assert.AreEqual("https://example.com/terms", partial.TermsUrl, "a valid value is trimmed and kept");
            Assert.AreEqual(LegalLinks.DefaultSupportEmail, partial.SupportEmail);
        }

        [Test]
        public void ParseSurvivesGarbageEmptyAndNull()
        {
            foreach (string json in new[] { null, "", "   ", "not json", "{", "[1,2]", "{\"privacyUrl\": 5}" })
            {
                LegalLinks.Config c = LegalLinks.Parse(json);
                Assert.AreEqual(LegalLinks.DefaultPrivacyUrl, c.PrivacyUrl, "input: " + json);
                Assert.AreEqual(LegalLinks.DefaultTermsUrl, c.TermsUrl, "input: " + json);
                Assert.AreEqual(LegalLinks.DefaultSupportEmail, c.SupportEmail, "input: " + json);
            }
        }

        [Test]
        public void IsWebUrlAcceptsOnlyAbsoluteHttpsPages()
        {
            Assert.IsTrue(LegalLinks.IsWebUrl("https://example.com"));
            Assert.IsTrue(LegalLinks.IsWebUrl("HTTPS://Example.com/Path?x=1"));
            Assert.IsTrue(LegalLinks.IsWebUrl("  https://example.com/a  "), "surrounding spaces are trimmed");
            Assert.IsFalse(LegalLinks.IsWebUrl(null));
            Assert.IsFalse(LegalLinks.IsWebUrl(""));
            Assert.IsFalse(LegalLinks.IsWebUrl("http://example.com"));
            Assert.IsFalse(LegalLinks.IsWebUrl("ftp://example.com"));
            Assert.IsFalse(LegalLinks.IsWebUrl("javascript:alert(1)"));
            Assert.IsFalse(LegalLinks.IsWebUrl("example.com/policy"));
            Assert.IsFalse(LegalLinks.IsWebUrl("https://"));
            Assert.IsFalse(LegalLinks.IsWebUrl("https://localhost"), "a bare host without a dot is not a public page");
            Assert.IsFalse(LegalLinks.IsWebUrl("https://exa mple.com"));
        }

        [Test]
        public void IsEmailIsAPlainMailboxCheck()
        {
            Assert.IsTrue(LegalLinks.IsEmail("a@b.co"));
            Assert.IsTrue(LegalLinks.IsEmail(" jpdash.creator@gmail.com "));
            Assert.IsFalse(LegalLinks.IsEmail(null));
            Assert.IsFalse(LegalLinks.IsEmail(""));
            Assert.IsFalse(LegalLinks.IsEmail("nomail"));
            Assert.IsFalse(LegalLinks.IsEmail("@b.com"));
            Assert.IsFalse(LegalLinks.IsEmail("a@b"));
            Assert.IsFalse(LegalLinks.IsEmail("a@b."));
            Assert.IsFalse(LegalLinks.IsEmail("a@@b.com"));
            Assert.IsFalse(LegalLinks.IsEmail("a b@c.com"));
        }

        [Test]
        public void ShippedConfigLoadsAsTwoHttpsPagesOnOneSite()
        {
            LegalLinks.Config c = LegalLinks.Current;   // reads Resources/Services/legal_config.json
            Assert.IsTrue(LegalLinks.IsWebUrl(c.PrivacyUrl), c.PrivacyUrl);
            Assert.IsTrue(LegalLinks.IsWebUrl(c.TermsUrl), c.TermsUrl);
            Assert.AreNotEqual(c.PrivacyUrl, c.TermsUrl);
            Assert.AreEqual(new Uri(c.PrivacyUrl).Host, new Uri(c.TermsUrl).Host, "both pages on the same site");
            Assert.IsTrue(c.PrivacyUrl.EndsWith("/policy.html"), "docs/policy.html is served as /policy.html: " + c.PrivacyUrl);
            Assert.IsTrue(c.TermsUrl.EndsWith("/terms.html"), "docs/terms.html is served as /terms.html: " + c.TermsUrl);
            Assert.IsTrue(LegalLinks.IsEmail(c.SupportEmail), c.SupportEmail);
            Assert.AreEqual(c.PrivacyUrl, LegalLinks.PrivacyUrl);
            Assert.AreEqual(c.TermsUrl, LegalLinks.TermsUrl);
            Assert.AreEqual(c.SupportEmail, LegalLinks.SupportEmail);
        }

        [Test]
        public void OpenGoesThroughTheOpenerAndRefusesAnythingButHttps()
        {
            var opened = new List<string>();
            LegalLinks.Opener = url => opened.Add(url);
            LegalLinks.SetForTests(new LegalLinks.Config("https://example.com/p", "https://example.com/t", "help@example.com"));

            LegalLinks.OpenPrivacy();
            LegalLinks.OpenTerms();
            Assert.AreEqual(new[] { "https://example.com/p", "https://example.com/t" }, opened.ToArray());

            Assert.IsFalse(LegalLinks.Open("http://example.com"), "http is refused");
            Assert.IsFalse(LegalLinks.Open("javascript:alert(1)"), "scripts are refused");
            Assert.IsFalse(LegalLinks.Open(null));
            Assert.AreEqual(2, opened.Count, "refused urls never reach the opener");

            Assert.IsTrue(LegalLinks.Open("  https://example.com/x "), "a valid url is trimmed and opened");
            Assert.AreEqual("https://example.com/x", opened[2]);
        }

        [Test]
        public void OpenNeverThrowsWhenTheOpenerFails()
        {
            LegalLinks.Opener = url => { throw new InvalidOperationException("no browser"); };
            Assert.IsFalse(LegalLinks.Open("https://example.com/p"));
        }

        [Test]
        public void ButtonWordsAreShortAndTheScreenReaderTextsSayTheGameIsLeft()
        {
            Assert.LessOrEqual(LegalLinks.PrivacyLabel.Length, 8, "fits half a Settings row at 130 percent text");
            Assert.LessOrEqual(LegalLinks.TermsLabel.Length, 8);
            StringAssert.Contains("browser", LegalLinks.PrivacyA11y);
            StringAssert.Contains("browser", LegalLinks.TermsA11y);
            StringAssert.Contains("Privacy policy", LegalLinks.PrivacyA11y);
            StringAssert.Contains("Terms of use", LegalLinks.TermsA11y);
        }
    }
}
