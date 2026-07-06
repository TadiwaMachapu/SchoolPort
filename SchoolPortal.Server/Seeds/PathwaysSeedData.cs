using Microsoft.EntityFrameworkCore;
using SchoolPortal.Data;
using SchoolPortal.Data.Entities;

namespace SchoolPortal.Server.Seeds;

public static class PathwaysSeedData
{
    public static async Task SeedAsync(SchoolPortalDbContext context, ILogger logger)
    {
        if (await context.Universities.AnyAsync())
        {
            // Base data already seeded — still run the Sprint 1.5.1 Gap 4 expansion, which is
            // additive by abbreviation (mirrors MatricHubSeedData's extended-requirements sync),
            // so already-seeded databases (including live) receive the new universities.
            await SeedExpandedUniversitiesAsync(context, logger);
            return;
        }

        logger.LogInformation("Seeding Pathways reference data…");

        // ── Careers ─────────────────────────────────────────────────────────────

        var c = new Dictionary<string, Career>
        {
            ["MedDoctor"]      = C("Medical Doctor",       "Health Sciences",  "Diagnoses and treats illness, injury, and disease."),
            ["Dentist"]        = C("Dentist",              "Health Sciences",  "Diagnoses and treats dental and oral health conditions."),
            ["Pharmacist"]     = C("Pharmacist",           "Health Sciences",  "Dispenses medications and advises on drug therapies."),
            ["Nurse"]          = C("Nurse",                "Health Sciences",  "Provides direct patient care and health education."),
            ["Physio"]         = C("Physiotherapist",      "Health Sciences",  "Rehabilitates patients through physical therapy."),
            ["CivilEng"]       = C("Civil Engineer",       "Engineering",      "Designs and oversees construction of infrastructure."),
            ["ElecEng"]        = C("Electrical Engineer",  "Engineering",      "Designs and develops electrical systems and equipment."),
            ["MechEng"]        = C("Mechanical Engineer",  "Engineering",      "Designs mechanical devices, machines, and systems."),
            ["SoftEng"]        = C("Software Engineer",    "Technology",       "Designs and builds software systems and applications."),
            ["DataSci"]        = C("Data Scientist",       "Technology",       "Extracts insights from large data sets using analytics."),
            ["Lawyer"]         = C("Lawyer",               "Law",              "Advises clients and represents them in legal matters."),
            ["CA"]             = C("Chartered Accountant", "Commerce",         "Manages financial records, audits, and tax compliance."),
            ["FinAnalyst"]     = C("Financial Analyst",    "Commerce",         "Analyses financial data to guide investment decisions."),
            ["Actuary"]        = C("Actuary",              "Commerce",         "Assesses financial risk using mathematics and statistics."),
            ["Teacher"]        = C("Teacher / Educator",   "Education",        "Teaches and guides learners in a school environment."),
            ["Psychologist"]   = C("Psychologist",         "Social Sciences",  "Studies behaviour and mental processes, provides therapy."),
            ["SocialWorker"]   = C("Social Worker",        "Social Sciences",  "Supports individuals and communities facing social challenges."),
            ["Architect"]      = C("Architect",            "Built Environment","Designs buildings and oversees their construction."),
            ["Journalist"]     = C("Journalist",           "Humanities",       "Researches and reports news across print, broadcast, and digital media."),
            ["Geologist"]      = C("Geologist",            "Natural Sciences", "Studies the Earth's structure, composition, and processes."),
        };

        context.Careers.AddRange(c.Values);

        // ── Universities ─────────────────────────────────────────────────────────

        var u = new Dictionary<string, University>
        {
            ["UCT"]  = U("University of Cape Town",                 "UCT",  "Western Cape",   "https://www.uct.ac.za"),
            ["Wits"] = U("University of the Witwatersrand",         "Wits", "Gauteng",         "https://www.wits.ac.za"),
            ["SU"]   = U("Stellenbosch University",                 "SU",   "Western Cape",   "https://www.sun.ac.za"),
            ["UP"]   = U("University of Pretoria",                  "UP",   "Gauteng",         "https://www.up.ac.za"),
            ["UJ"]   = U("University of Johannesburg",              "UJ",   "Gauteng",         "https://www.uj.ac.za"),
            ["UKZN"] = U("University of KwaZulu-Natal",             "UKZN", "KwaZulu-Natal",   "https://www.ukzn.ac.za"),
            ["NWU"]  = U("North-West University",                   "NWU",  "North West",      "https://www.nwu.ac.za"),
            ["TUT"]  = U("Tshwane University of Technology",        "TUT",  "Gauteng",         "https://www.tut.ac.za"),
            ["CPUT"] = U("Cape Peninsula University of Technology", "CPUT", "Western Cape",   "https://www.cput.ac.za"),
            ["RU"]   = U("Rhodes University",                       "RU",   "Eastern Cape",    "https://www.ru.ac.za"),
        };

        context.Universities.AddRange(u.Values);

        // ── Courses ──────────────────────────────────────────────────────────────

        void AddCourse(
            University uni, Career career, string name, string faculty, int aps, string? apsNotes,
            params (string subj, int? minPct, bool req, string? note)[] reqs)
        {
            var course = new UniversityCourse
            {
                UniversityCourseId = Guid.NewGuid(),
                UniversityId = uni.UniversityId,
                CareerId = career.CareerId,
                Name = name,
                Faculty = faculty,
                MinimumAps = aps,
                ApsNotes = apsNotes
            };
            context.UniversityCourses.Add(course);

            foreach (var (subj, minPct, isReq, note) in reqs)
            {
                context.CourseSubjectRequirements.Add(new CourseSubjectRequirement
                {
                    CourseSubjectRequirementId = Guid.NewGuid(),
                    UniversityCourseId = course.UniversityCourseId,
                    SubjectName = subj,
                    MinimumPercent = minPct,
                    IsRequired = isReq,
                    Notes = note
                });
            }
        }

        // UCT
        AddCourse(u["UCT"], c["MedDoctor"], "MBChB (Medicine)", "Health Sciences", 36,
            "APS excludes Life Orientation. Maths Literacy not accepted.",
            ("Mathematics",           70, true,  "Maths Literacy not accepted"),
            ("Physical Sciences",     70, true,  null),
            ("English Home Language", 60, true,  null));

        AddCourse(u["UCT"], c["SoftEng"], "BSc Computer Science", "Science", 36, null,
            ("Mathematics", 70, true, null));

        AddCourse(u["UCT"], c["CA"], "BCom Accounting", "Commerce", 36, null,
            ("Mathematics",           60, true, "Maths Literacy not accepted"),
            ("English Home Language", 50, true, null));

        AddCourse(u["UCT"], c["Lawyer"], "LLB (Bachelor of Laws)", "Law", 34, null,
            ("English Home Language", 60, true, null));

        AddCourse(u["UCT"], c["CivilEng"], "BSc Civil Engineering", "Engineering & Built Environment", 36, null,
            ("Mathematics",       70, true, null),
            ("Physical Sciences", 70, true, null));

        // Wits
        AddCourse(u["Wits"], c["MedDoctor"], "MBBCh (Medicine)", "Health Sciences", 37,
            "APS excludes Life Orientation.",
            ("Mathematics",       70, true, null),
            ("Physical Sciences", 60, true, null),
            ("Life Sciences",     60, true, null));

        AddCourse(u["Wits"], c["SoftEng"], "BSc Computer Science", "Science", 34, null,
            ("Mathematics", 60, true, null));

        AddCourse(u["Wits"], c["CA"], "BCom Accounting", "Commerce, Law and Management", 34, null,
            ("Mathematics", 60, true, null));

        AddCourse(u["Wits"], c["Lawyer"], "LLB (Bachelor of Laws)", "Law", 32, null,
            ("English Home Language", 60, true, null));

        AddCourse(u["Wits"], c["ElecEng"], "BSc Electrical Engineering", "Engineering & Built Sciences", 34, null,
            ("Mathematics",       70, true, null),
            ("Physical Sciences", 60, true, null));

        // Stellenbosch
        AddCourse(u["SU"], c["MedDoctor"], "MBChB (Medicine)", "Medicine and Health Sciences", 36, null,
            ("Mathematics",       70, true, null),
            ("Physical Sciences", 70, true, null));

        AddCourse(u["SU"], c["CivilEng"], "BEng Civil Engineering", "Engineering", 34, null,
            ("Mathematics",       70, true, null),
            ("Physical Sciences", 60, true, null));

        AddCourse(u["SU"], c["CA"], "BCom Accounting", "Economic and Management Sciences", 34, null,
            ("Mathematics", 60, true, null));

        AddCourse(u["SU"], c["Journalist"], "BA Journalism", "Arts and Social Sciences", 30, null,
            ("English Home Language", 60, true, null));

        AddCourse(u["SU"], c["Actuary"], "BSc Actuarial Science", "Economic and Management Sciences", 36,
            "Mathematics minimum is very competitive; distinction advised.",
            ("Mathematics", 80, true, null));

        // UP
        AddCourse(u["UP"], c["MedDoctor"], "MBChB (Medicine)", "Health Sciences", 34, null,
            ("Mathematics",       70, true,  null),
            ("Physical Sciences", 70, true,  null),
            ("Life Sciences",     60, false, "Recommended"));

        AddCourse(u["UP"], c["MechEng"], "BEng Mechanical Engineering", "Engineering, Built Environment and IT", 32, null,
            ("Mathematics",       70, true, null),
            ("Physical Sciences", 60, true, null));

        AddCourse(u["UP"], c["SoftEng"], "BSc Computer Science", "Engineering, Built Environment and IT", 30, null,
            ("Mathematics", 60, true, null));

        AddCourse(u["UP"], c["CA"], "BCom Accounting Sciences", "Economic and Management Sciences", 28, null,
            ("Mathematics", 60, true, null));

        AddCourse(u["UP"], c["Lawyer"], "LLB (Bachelor of Laws)", "Law", 28, null,
            ("English Home Language", 50, true, null));

        // UJ
        AddCourse(u["UJ"], c["SoftEng"], "BSc Computer Science", "Science", 30, null,
            ("Mathematics", 60, true, null));

        AddCourse(u["UJ"], c["CA"], "BCom Accounting", "Accounting and Informatics", 26, null,
            ("Mathematics", 50, true, null));

        AddCourse(u["UJ"], c["Journalist"], "BA Journalism", "Humanities", 24, null,
            ("English Home Language", 50, true, null));

        AddCourse(u["UJ"], c["Teacher"], "BEd Foundation Phase Teaching", "Education", 22, null,
            ("English Home Language", 40, true, null));

        AddCourse(u["UJ"], c["Nurse"], "BCur Nursing", "Health Sciences", 26, null,
            ("Life Sciences",         50, true, null),
            ("English Home Language", 40, true, null));

        // UKZN
        AddCourse(u["UKZN"], c["MedDoctor"], "MBChB (Medicine)", "Health Sciences", 36, null,
            ("Mathematics",       70, true, null),
            ("Physical Sciences", 70, true, null));

        AddCourse(u["UKZN"], c["CA"], "BCom Accounting", "Management Studies", 28, null,
            ("Mathematics", 50, true, null));

        AddCourse(u["UKZN"], c["Teacher"], "BEd Education", "Education", 24, null,
            ("English Home Language", 40, true, null));

        AddCourse(u["UKZN"], c["Psychologist"], "BA Psychology", "Humanities", 30, null,
            ("English Home Language", 50, true, null));

        AddCourse(u["UKZN"], c["DataSci"], "BSc Data Science", "Science and Agriculture", 28, null,
            ("Mathematics", 60, true, null));

        // NWU
        AddCourse(u["NWU"], c["SoftEng"], "BSc Computer Science", "Natural and Agricultural Sciences", 28, null,
            ("Mathematics", 60, true, null));

        AddCourse(u["NWU"], c["CA"], "BCom Accounting Sciences", "Economic and Management Sciences", 26, null,
            ("Mathematics", 50, true, null));

        AddCourse(u["NWU"], c["Teacher"], "BEd Intermediate Phase", "Education", 22, null,
            ("English Home Language", 40, true, null));

        AddCourse(u["NWU"], c["Geologist"], "BSc Geology", "Natural and Agricultural Sciences", 26, null,
            ("Mathematics",       50, true, null),
            ("Physical Sciences", 50, true, null));

        AddCourse(u["NWU"], c["Physio"], "BSc Physiotherapy", "Health Sciences", 28, null,
            ("Life Sciences",     60, true,  null),
            ("Physical Sciences", 50, false, "Recommended"));

        // TUT
        AddCourse(u["TUT"], c["SoftEng"], "ND: Information Technology", "Information and Communication Technology", 20, null,
            ("Mathematics", 40, true, null));

        AddCourse(u["TUT"], c["CivilEng"], "ND: Civil Engineering", "Engineering", 20, null,
            ("Mathematics",       40, true, null),
            ("Physical Sciences", 40, true, null));

        AddCourse(u["TUT"], c["CA"], "ND: Accounting", "Management Sciences", 18, null,
            ("Mathematics", 40, true, null));

        AddCourse(u["TUT"], c["Journalist"], "ND: Journalism", "Humanities", 18, null,
            ("English Home Language", 40, true, null));

        AddCourse(u["TUT"], c["ElecEng"], "BTech: Electrical Engineering", "Engineering", 24, null,
            ("Mathematics",       50, true, null),
            ("Physical Sciences", 50, true, null));

        // CPUT
        AddCourse(u["CPUT"], c["SoftEng"], "ND: Information Technology", "Informatics & Design", 20, null,
            ("Mathematics", 40, true, null));

        AddCourse(u["CPUT"], c["CivilEng"], "ND: Civil Engineering", "Engineering", 20, null,
            ("Mathematics",       40, true, null),
            ("Physical Sciences", 40, true, null));

        AddCourse(u["CPUT"], c["Nurse"], "ND: Nursing", "Health and Wellness Sciences", 20, null,
            ("Life Sciences",         40, true, null),
            ("English Home Language", 40, true, null));

        AddCourse(u["CPUT"], c["Architect"], "BTech: Architectural Technology", "Informatics & Design", 24, null,
            ("Mathematics", 50, true, null));

        AddCourse(u["CPUT"], c["CA"], "ND: Accounting", "Business and Management Sciences", 18, null,
            ("Mathematics", 40, true, null));

        // Rhodes
        AddCourse(u["RU"], c["Journalist"], "BA Journalism & Media Studies", "Humanities", 28, null,
            ("English Home Language", 60, true, null));

        AddCourse(u["RU"], c["Psychologist"], "BA Psychology", "Humanities", 28, null,
            ("English Home Language", 50, true, null));

        AddCourse(u["RU"], c["SoftEng"], "BSc Computer Science", "Science", 30, null,
            ("Mathematics", 60, true, null));

        AddCourse(u["RU"], c["SocialWorker"], "BA Social Work", "Humanities", 24, null,
            ("English Home Language", 50, true, null));

        AddCourse(u["RU"], c["Geologist"], "BSc Environmental Science", "Science", 26, null,
            ("Mathematics",   50, true, null),
            ("Life Sciences", 50, true, null));

        // ── Senior Phase Requirements ─────────────────────────────────────────────

        context.SeniorPhaseRequirements.AddRange(
            new SeniorPhaseRequirement { SeniorPhaseRequirementId = Guid.NewGuid(), FetSubjectName = "Mathematics",       RequiredSeniorPhaseSubjectName = "Mathematics",                    RecommendedMinPercent = 50, Notes = "Strong Gr 9 Maths foundation is essential for FET Mathematics." },
            new SeniorPhaseRequirement { SeniorPhaseRequirementId = Guid.NewGuid(), FetSubjectName = "Physical Sciences", RequiredSeniorPhaseSubjectName = "Natural Sciences",               RecommendedMinPercent = 50, Notes = "Natural Sciences covers Physics and Chemistry foundations." },
            new SeniorPhaseRequirement { SeniorPhaseRequirementId = Guid.NewGuid(), FetSubjectName = "Life Sciences",     RequiredSeniorPhaseSubjectName = "Natural Sciences",               RecommendedMinPercent = 50, Notes = "Biology component of Natural Sciences is the foundation." },
            new SeniorPhaseRequirement { SeniorPhaseRequirementId = Guid.NewGuid(), FetSubjectName = "Accounting",        RequiredSeniorPhaseSubjectName = "Economic and Management Sciences", RecommendedMinPercent = 50, Notes = "EMS in Gr 7–9 introduces basic financial concepts." },
            new SeniorPhaseRequirement { SeniorPhaseRequirementId = Guid.NewGuid(), FetSubjectName = "Geography",         RequiredSeniorPhaseSubjectName = "Social Sciences",                RecommendedMinPercent = 50, Notes = "Geography component of Social Sciences is the foundation." },
            new SeniorPhaseRequirement { SeniorPhaseRequirementId = Guid.NewGuid(), FetSubjectName = "History",           RequiredSeniorPhaseSubjectName = "Social Sciences",                RecommendedMinPercent = 50, Notes = "History component of Social Sciences is the foundation." }
        );

        await context.SaveChangesAsync();
        logger.LogInformation("Pathways seed complete: {Unis} universities, {Careers} careers",
            u.Count, c.Count);

        await SeedExpandedUniversitiesAsync(context, logger);
    }

    /// <summary>
    /// Sprint 1.5.1 Gap 4 — expansion from 10 to all 26 SA public universities. Additive
    /// sync-by-abbreviation: runs on every startup, adds only universities not yet present, so
    /// already-seeded databases (including live) converge with a fresh install. Every block cites
    /// the official source URL its numbers were verified against (2026 intake documents unless the
    /// ApsNotes carries a cycle caveat). Universities publishing their own weighted point scales
    /// (NMU/UWC/SPU) or subject-levels-only (DUT/CUT/MUT) seed MinimumAps = 0 with the full
    /// published requirement in ApsNotes — never an invented APS-equivalent; goal tracking then
    /// derives status from subject gaps only (approved 2026-07-06).
    /// </summary>
    private static async Task SeedExpandedUniversitiesAsync(SchoolPortalDbContext context, ILogger logger)
    {
        var existing = await context.Universities.Select(x => x.Abbreviation).ToListAsync();
        var careers = await context.Careers.ToDictionaryAsync(x => x.Name);
        Career? Career(string name) => careers.GetValueOrDefault(name);
        var added = 0;
        var coursesAdded = 0;

        void AddCourse(University uni, Career? career, string name, string faculty, int aps,
            string? apsNotes, params (string subj, int? minPct, bool req, string? note)[] reqs)
        {
            var course = new UniversityCourse
            {
                UniversityCourseId = Guid.NewGuid(),
                UniversityId = uni.UniversityId,
                CareerId = career?.CareerId,
                Name = name,
                Faculty = faculty,
                MinimumAps = aps,
                ApsNotes = apsNotes,
            };
            context.UniversityCourses.Add(course);
            coursesAdded++;

            foreach (var (subj, minPct, isReq, note) in reqs)
                context.CourseSubjectRequirements.Add(new CourseSubjectRequirement
                {
                    CourseSubjectRequirementId = Guid.NewGuid(),
                    UniversityCourseId = course.UniversityCourseId,
                    SubjectName = subj,
                    MinimumPercent = minPct,
                    IsRequired = isReq,
                    Notes = note,
                });
        }

        University? Uni(string name, string abbr, string province, string website)
        {
            if (existing.Contains(abbr)) return null; // already present — skip whole block
            var uni = U(name, abbr, province, website);
            context.Universities.Add(uni);
            added++;
            return uni;
        }

        // ── UFS — University of the Free State ──────────────────────────────────
        // Source: https://www.ufs.ac.za/docs/librariesprovider44/study-at-the-ufs/2026-ufs-prospectus.pdf
        //         (BSc rule: https://www.ufs.ac.za/docs/librariesprovider44/undergraduate/ug-addendum.pdf)
        if (Uni("University of the Free State", "UFS", "Free State", "https://www.ufs.ac.za") is { } ufs)
        {
            AddCourse(ufs, Career("Medical Doctor"), "MBChB (Medicine)", "Health Sciences", 36,
                "Subject to selection; NBT required. AP per 2026 prospectus.",
                ("English Home Language", 60, true, null),
                ("Mathematics",           60, true, null),
                ("Physical Sciences",     60, true, null),
                ("Life Sciences",         60, true, null));
            AddCourse(ufs, Career("Lawyer"), "LLB (Bachelor of Laws)", "Law", 33, null,
                ("English Home Language", 70, true, null),
                ("Mathematics",           50, true, "Or Mathematical Literacy 70%"));
            AddCourse(ufs, Career("Chartered Accountant"), "BAcc (Accounting)", "Economic and Management Sciences", 34,
                "SAICA-accredited CA route.",
                ("English Home Language", 50, true, null),
                ("Mathematics",           60, true, null));
            AddCourse(ufs, Career("Financial Analyst"), "BCom", "Economic and Management Sciences", 28, null,
                ("English Home Language", 50, true, null),
                ("Mathematics",           50, true, null));
            AddCourse(ufs, Career("Nurse"), "Bachelor of Nursing", "Health Sciences", 30,
                "Subject to selection; subject levels per 2026 prospectus.",
                ("English Home Language", 50, true, null));
            AddCourse(ufs, Career("Software Engineer"), "BSc (Computer Science stream)", "Natural and Agricultural Sciences", 32,
                "Faculty-wide minimum AP 32 for all BSc programmes (2026 addendum).",
                ("Mathematics", 60, true, null));
            AddCourse(ufs, Career("Teacher / Educator"), "BEd Foundation Phase", "Education", 30, null,
                ("English Home Language", 50, true, null));
        }

        // ── UL — University of Limpopo ──────────────────────────────────────────
        // Source: https://www.ul.ac.za/wp-content/uploads/2025/03/Undergraduate-Prospectus-2027.pdf
        if (Uni("University of Limpopo", "UL", "Limpopo", "https://www.ul.ac.za") is { } ul)
        {
            const string ulCycle = "Requirements from 2027 prospectus.";
            AddCourse(ul, Career("Medical Doctor"), "MBChB (Medicine)", "Health Sciences", 27,
                $"6-year programme; selection applies. {ulCycle}",
                ("English Home Language", 50, true, null),
                ("Mathematics",           60, true, null),
                ("Physical Sciences",     60, true, null),
                ("Life Sciences",         60, true, null));
            AddCourse(ul, Career("Lawyer"), "LLB (Bachelor of Laws)", "Management and Law", 30,
                $"English 60% (50% accepted for English Home Language). {ulCycle}",
                ("English Home Language", 60, true, null));
            AddCourse(ul, Career("Chartered Accountant"), "BAcc (Accountancy)", "Management and Law", 30,
                ulCycle,
                ("English Home Language", 50, true, null),
                ("Mathematics",           50, true, null));
            AddCourse(ul, Career("Financial Analyst"), "BCom (Accountancy)", "Management and Law", 28,
                ulCycle,
                ("English Home Language", 50, true, null),
                ("Mathematics",           50, true, null));
            AddCourse(ul, Career("Nurse"), "Bachelor of Nursing", "Health Sciences", 26,
                ulCycle,
                ("English Home Language", 50, true, null),
                ("Mathematics",           50, true, null),
                ("Physical Sciences",     60, true, null),
                ("Life Sciences",         60, true, null));
            AddCourse(ul, Career("Data Scientist"), "BSc (Mathematical Sciences stream)", "Science and Agriculture", 24,
                ulCycle,
                ("English Home Language", 50, true, null),
                ("Mathematics",           60, true, null));
        }

        // ── UNIVEN — University of Venda ────────────────────────────────────────
        // Source: https://www.univen.ac.za/wp-content/uploads/2025/04/2026-UNIVEN-Undergraduate-Student-Information-Brochure.pdf
        if (Uni("University of Venda", "UNIVEN", "Limpopo", "https://www.univen.ac.za") is { } univen)
        {
            AddCourse(univen, Career("Lawyer"), "LLB (Bachelor of Laws)", "Law", 38, null,
                ("English Home Language", 60, true, null));
            AddCourse(univen, Career("Nurse"), "Bachelor of Nursing", "Health Sciences", 36, null,
                ("English Home Language", 50, true, null),
                ("Mathematics",           50, true, null),
                ("Physical Sciences",     50, true, null),
                ("Life Sciences",         50, true, null));
            AddCourse(univen, Career("Teacher / Educator"), "BEd Foundation Phase Teaching", "Education", 36, null,
                ("English Home Language", 50, true, null));
            AddCourse(univen, Career("Chartered Accountant"), "BCom Accounting Sciences", "Management, Commerce and Law", 35,
                "Institutional minimum point score is 26.",
                ("English Home Language", 50, true, null),
                ("Accounting",            50, true, null));
        }

        // ── UNIZULU — University of Zululand ────────────────────────────────────
        // Source: https://www.unizulu.ac.za/wp-content/uploads/2026/01/FCAL-Handbook-2026.pdf
        if (Uni("University of Zululand", "UNIZULU", "KwaZulu-Natal", "https://www.unizulu.ac.za") is { } unizulu)
        {
            AddCourse(unizulu, Career("Lawyer"), "LLB (Bachelor of Laws)", "Commerce, Administration and Law", 30,
                "Strict enrolment quotas apply.",
                ("English Home Language", 50, true, null),
                ("Mathematics",           40, true, "Or Mathematical Literacy 50%"));
            AddCourse(unizulu, Career("Financial Analyst"), "BCom", "Commerce, Administration and Law", 28, null,
                ("English Home Language", 50, true, null),
                ("Mathematics",           40, true, "Or Mathematical Literacy 70%"));
            AddCourse(unizulu, Career("Chartered Accountant"), "BCom (Accounting)", "Commerce, Administration and Law", 28, null,
                ("English Home Language", 50, true, null),
                ("Mathematics",           50, true, null));
        }

        // ── WSU — Walter Sisulu University ──────────────────────────────────────
        // Source: https://www.wsu.ac.za/media/attachments/2025/06/10/wsu-2026-undergraduate-admission-requirements.pdf
        if (Uni("Walter Sisulu University", "WSU", "Eastern Cape", "https://www.wsu.ac.za") is { } wsu)
        {
            AddCourse(wsu, Career("Medical Doctor"), "MBChB (Bachelor of Medicine and Bachelor of Surgery)", "Medicine and Health Sciences", 30,
                "Additional selection criteria apply.",
                ("English Home Language", 60, true, null),
                ("Mathematics",           60, true, null),
                ("Physical Sciences",     60, true, null),
                ("Life Sciences",         60, true, null));
            AddCourse(wsu, Career("Nurse"), "Bachelor of Nursing", "Medicine and Health Sciences", 24,
                "Additional selection criteria apply.",
                ("English Home Language", 50, true, null),
                ("Mathematics",           50, true, null),
                ("Physical Sciences",     50, true, null),
                ("Life Sciences",         50, true, null));
            AddCourse(wsu, Career("Lawyer"), "LLB (Bachelor of Laws)", "Law, Humanities and Social Sciences", 26, null,
                ("English Home Language", 60, true, null),
                ("Mathematics",           40, true, "Or Mathematical Literacy 50%"));
            AddCourse(wsu, Career("Chartered Accountant"), "Bachelor of Accounting Sciences", "Commerce", 27,
                "CA (SA) route.",
                ("English Home Language", 60, true, null),
                ("Mathematics",           50, true, null));
            AddCourse(wsu, Career("Financial Analyst"), "BCom", "Commerce", 25, null,
                ("English Home Language", 50, true, null),
                ("Mathematics",           40, true, "Or Mathematical Literacy 60%"));
            AddCourse(wsu, Career("Software Engineer"), "BSc Computer Science", "Natural Sciences", 25,
                "23 for the extended stream.",
                ("Mathematics", 50, true, null));
        }

        // ── SMU — Sefako Makgatho Health Sciences University ────────────────────
        // Source: https://www.smu.ac.za/schools/medicine/medicine-undergraduate-admission-requirements/
        if (Uni("Sefako Makgatho Health Sciences University", "SMU", "Gauteng", "https://www.smu.ac.za") is { } smu)
        {
            AddCourse(smu, Career("Medical Doctor"), "MBChB (Medicine)", "Medicine", 38,
                "Health-sciences-only university. Three-phase selection by merit.",
                ("English Home Language", 70, true, null),
                ("Mathematics",           70, true, null),
                ("Physical Sciences",     70, true, null),
                ("Life Sciences",         70, true, null));
            AddCourse(smu, null, "Bachelor of Radiography (Diagnostic)", "Medicine", 25,
                "Pre-selection requires combined score of 19 in the four listed subjects.",
                ("English Home Language", 50, true, null),
                ("Mathematics",           50, true, null),
                ("Physical Sciences",     50, true, null),
                ("Life Sciences",         50, true, null));
        }

        // ── VUT — Vaal University of Technology ─────────────────────────────────
        // Source: https://vut.ac.za/wp-content/uploads/2026/03/2027-Undergraduate-Minimum-Admission-Requirements.pdf
        if (Uni("Vaal University of Technology", "VUT", "Gauteng", "https://vut.ac.za") is { } vut)
        {
            const string vutCycle = "Requirements from 2027 prospectus.";
            AddCourse(vut, Career("Civil Engineer"), "Diploma: Engineering (Civil/Mechanical/Chemical/Electronic/Computer Systems)", "Engineering and Technology", 24,
                $"APS excludes LO. 22 for 4-year extended programmes. {vutCycle}",
                ("English Home Language", 50, true, null),
                ("Mathematics",           50, true, null),
                ("Physical Sciences",     50, true, null));
            AddCourse(vut, Career("Software Engineer"), "Diploma: Information Technology", "Applied and Computer Sciences", 26,
                $"APS 28 with Mathematical Literacy (level 6). {vutCycle}",
                ("English Home Language", 50, true, null),
                ("Mathematics",           50, true, "Or Mathematical Literacy 70%"));
            AddCourse(vut, null, "BHSc: Medical Laboratory Science", "Applied and Computer Sciences", 27,
                vutCycle,
                ("English Home Language", 50, true, null),
                ("Mathematics",           50, true, null),
                ("Physical Sciences",     50, true, null),
                ("Life Sciences",         60, true, null));
            AddCourse(vut, Career("Teacher / Educator"), "BEd (Senior Phase & FET Teaching)", "Human Sciences", 22,
                $"APS 24 with Mathematical Literacy (level 6). {vutCycle}",
                ("English Home Language", 50, true, "Language of teaching and learning"),
                ("Mathematics",           50, true, "Or Mathematical Literacy 70%"),
                ("Physical Sciences",     40, true, null));
            AddCourse(vut, Career("Chartered Accountant"), "Diploma: Cost and Management Accounting", "Management Sciences", 20,
                $"APS 22 with Mathematical Literacy (level 5). {vutCycle}",
                ("Accounting",            50, true, null),
                ("English Home Language", 50, true, null),
                ("Mathematics",           40, true, null));
        }

        // ── UMP — University of Mpumalanga ──────────────────────────────────────
        // Sources: https://www.ump.ac.za/Study-with-us/Faculties-and-Schools/Faculty-of-Economics,-Development-and-Business-Sci/School-of-Development-Studies/Bachelor-of-Laws.aspx
        //          https://www.ump.ac.za/Study-with-us/Faculties-and-Schools/Faculty-of-Education/School-of-Early-Childhood-Education/Bachelor-of-Education-in-Foundation-Phase-Teaching.aspx
        if (Uni("University of Mpumalanga", "UMP", "Mpumalanga", "https://www.ump.ac.za") is { } ump)
        {
            AddCourse(ump, Career("Lawyer"), "LLB (Bachelor of Laws)", "Economics, Development and Business Sciences", 33, null,
                ("English Home Language", 50, true, null),
                ("Mathematics",           40, true, "Or Mathematical Literacy 50%"));
            AddCourse(ump, Career("Teacher / Educator"), "BEd Foundation Phase Teaching", "Education", 26,
                "APS 27 with Mathematical Literacy. UMP counts Life Orientation ÷ 2 in its APS.",
                ("English Home Language", 60, true, "Language of teaching and learning level 5"),
                ("Mathematics",           50, true, "Or Mathematical Literacy 50%"));
        }

        // ── UNISA — University of South Africa ──────────────────────────────────
        // Sources: https://www.unisa.ac.za/sites/corporate/default/Apply-for-admission/Undergraduate-qualifications/Qualifications/All-qualifications/Bachelor-of-Laws-(98680-%E2%80%93-NEW)
        //          https://www.unisa.ac.za/sites/corporate/default/Apply-for-admission/Undergraduate-qualifications/Qualifications/All-qualifications/Bachelor-of-Commerce-(98314-%E2%80%93-GEN)
        if (Uni("University of South Africa", "UNISA", "Gauteng", "https://www.unisa.ac.za") is { } unisa)
        {
            const string unisaNote = "UNISA is open-distance; admission is per-qualification, not per-faculty.";
            AddCourse(unisa, Career("Lawyer"), "LLB (Bachelor of Laws) (98680)", "Law", 20,
                unisaNote,
                ("English Home Language", 50, true, "50% in the language of teaching and learning"));
            AddCourse(unisa, Career("Financial Analyst"), "BCom (98314)", "Economic and Management Sciences", 21,
                unisaNote,
                ("English Home Language", 50, true, null),
                ("Mathematics",           50, true, null));
            AddCourse(unisa, Career("Teacher / Educator"), "BEd Foundation Phase Teaching (90102)", "Education", 0,
                $"{unisaNote} Qualification page publishes subject requirements without an APS: 50% language of teaching, 40% Mathematics or 50% Mathematical Literacy.",
                ("English Home Language", 50, true, null),
                ("Mathematics",           40, true, "Or Mathematical Literacy 50%"));
        }

        // ── NMU — Nelson Mandela University (own Applicant Score out of 600) ────
        // Source: https://publications.mandela.ac.za/publications/media/Store/documents/Prospective%20students/2026-MandelaUni-Undergraduate-Guide.pdf
        if (Uni("Nelson Mandela University", "NMU", "Eastern Cape", "https://www.mandela.ac.za") is { } nmu)
        {
            const string nmuScale = "NMU uses an Applicant Score (AS) out of 600 (sum of six subject percentages), not standard APS — no APS equivalent seeded.";
            AddCourse(nmu, Career("Medical Doctor"), "MBChB (Medicine)", "Health Sciences", 0,
                $"AS 430 required. {nmuScale}",
                ("English Home Language", 60, true, null),
                ("Mathematics",           60, true, null),
                ("Physical Sciences",     60, true, null),
                ("Life Sciences",         60, true, null));
            AddCourse(nmu, Career("Financial Analyst"), "BCom General (Economics)", "Business and Economic Sciences", 0,
                $"AS 390 required. {nmuScale}",
                ("Mathematics", 50, true, null));
            AddCourse(nmu, Career("Chartered Accountant"), "BCom Accounting Sciences (CA route)", "Business and Economic Sciences", 0,
                $"AS 410 required. {nmuScale}",
                ("Mathematics", 50, true, null));
        }

        // ── UWC — University of the Western Cape (own weighted points system) ───
        // Source: https://uwc-za.b-cdn.net/files/files/Admission-Requirements-2025.pdf
        if (Uni("University of the Western Cape", "UWC", "Western Cape", "https://www.uwc.ac.za") is { } uwc)
        {
            const string uwcScale = "UWC uses a weighted points system (English/Maths up to 15 points each), not standard APS — no APS equivalent seeded. Requirements from 2025 edition. Verify at uwc.ac.za for current intake.";
            AddCourse(uwc, null, "Bachelor of Dental Surgery (BDS)", "Dentistry", 0,
                $"40 UWC points required. {uwcScale}",
                ("Mathematics",       50, true, null),
                ("Physical Sciences", 50, true, null),
                ("Life Sciences",     50, true, null));
            AddCourse(uwc, Career("Lawyer"), "LLB (Bachelor of Laws) (4-year)", "Law", 0,
                $"37 UWC points required. {uwcScale}",
                ("English Home Language", 50, true, null),
                ("Mathematics",           40, true, "Or Mathematical Literacy 60%"));
            AddCourse(uwc, Career("Software Engineer"), "BSc Computer Science", "Natural Sciences", 0,
                $"33 UWC points required (faculty minimum). {uwcScale}",
                ("Mathematics", 60, true, "Plus Physical/Life Sciences or IT at 50%"));
            AddCourse(uwc, Career("Nurse"), "B Nursing and Midwifery", "Community and Health Sciences", 0,
                $"30 UWC points required. {uwcScale}",
                ("Mathematics",   50, true, "Or Mathematical Literacy 70%"),
                ("Life Sciences", 50, true, null));
            AddCourse(uwc, Career("Financial Analyst"), "BCom", "Economic and Management Sciences", 0,
                $"30 UWC points required. {uwcScale}",
                ("Mathematics", 50, true, null));
        }

        // ── SPU — Sol Plaatje University (own weighted SPU points) ──────────────
        // Source: https://www.spu.ac.za/wp-content/uploads/2025/04/2026-Undergraduate-Prospectus.pdf
        if (Uni("Sol Plaatje University", "SPU", "Northern Cape", "https://www.spu.ac.za") is { } spu)
        {
            const string spuScale = "SPU points scale awards bonus points for Mathematics/Home Language and counts Life Orientation — not standard APS; no APS equivalent seeded.";
            AddCourse(spu, Career("Chartered Accountant"), "BCom in Accounting", "Economic and Management Sciences", 0,
                $"30 SPU points required. {spuScale}",
                ("English Home Language", 50, true, "Or English FAL at 60%"),
                ("Mathematics",           60, true, "Or Mathematics 50% plus Accounting 40%"));
            AddCourse(spu, Career("Financial Analyst"), "BCom in Economics", "Economic and Management Sciences", 0,
                $"30 SPU points required. {spuScale}",
                ("English Home Language", 50, true, "Or English FAL at 60%"),
                ("Mathematics",           60, true, "Or Mathematics 50% plus Economics/Business Studies 40%"));
            AddCourse(spu, Career("Teacher / Educator"), "BEd Foundation Phase Teaching", "Education", 0,
                $"30 SPU points required. {spuScale}",
                ("English Home Language", 50, true, "Or English FAL at 60%"),
                ("Mathematics",           40, true, "Or Mathematical Literacy 50%"));
        }

        // ── DUT — Durban University of Technology (subject levels only) ─────────
        // Source: https://www.dut.ac.za/wp-content/uploads/prospective_and_current_students/entry_requirements.pdf
        if (Uni("Durban University of Technology", "DUT", "KwaZulu-Natal", "https://www.dut.ac.za") is { } dut)
        {
            const string dutScale = "DUT publishes subject-level requirements with a ranking system, not APS totals — no APS equivalent seeded.";
            AddCourse(dut, Career("Civil Engineer"), "Diploma: Engineering (Civil)", "Engineering and the Built Environment", 0,
                dutScale,
                ("English Home Language", 50, true, null),
                ("Mathematics",           50, true, null),
                ("Physical Sciences",     50, true, null));
            AddCourse(dut, Career("Nurse"), "Bachelor of Nursing Science", "Health Sciences", 0,
                dutScale,
                ("English Home Language", 40, true, "English FAL 50%"),
                ("Mathematics",           50, true, "Or Mathematical Literacy 50%"),
                ("Life Sciences",         50, true, null));
            AddCourse(dut, Career("Software Engineer"), "Diploma: Information Technology", "Accounting and Informatics", 0,
                dutScale,
                ("English Home Language", 40, true, "English FAL 50%"),
                ("Mathematics",           40, true, "Or Mathematical Literacy 60%"));
        }

        // ── CUT — Central University of Technology (own 8-point CUT scale) ──────
        // Sources: https://www.cut.ac.za/admission-points-ap
        //          https://www.cut.ac.za/programmes/information-technology-2
        if (Uni("Central University of Technology", "CUT", "Free State", "https://www.cut.ac.za") is { } cut)
        {
            AddCourse(cut, Career("Software Engineer"), "Higher Certificate: Information Technology", "Engineering, Built Environment and IT", 0,
                "27 CUT points required (CUT's own 8-point scale, not standard APS). Institutional rule: below 22 not admitted; 22–26 requires a selection test. English at NQF level 4.",
                ("English Home Language", 50, true, null));
        }

        // ── MUT — Mangosuthu University of Technology (best-5 points aggregate) ─
        // Source: https://www.mut.ac.za/wp-content/uploads/2025/12/2026-Engineering-Faculty-Prospectus_.pdf
        if (Uni("Mangosuthu University of Technology", "MUT", "KwaZulu-Natal", "https://www.mut.ac.za") is { } mut)
        {
            AddCourse(mut, null, "Diploma: Chemical Engineering", "Engineering", 0,
                "MUT uses a best-five-subjects points aggregate; totals are faculty-specific and not published as APS — no APS equivalent seeded. All admissions via selection.",
                ("English Home Language", 50, true, null),
                ("Mathematics",           50, true, null),
                ("Physical Sciences",     50, true, null));
        }

        // ── UFH — University of Fort Hare (institutional minimum only) ──────────
        // Source: https://www.ufh.ac.za/wp-content/uploads/2026/01/GENERAL-PROSPECTUS-2026.pdf
        if (Uni("University of Fort Hare", "UFH", "Eastern Cape", "https://www.ufh.ac.za") is { } ufh)
        {
            AddCourse(ufh, null, "General Bachelor's Degree", "All Faculties", 26,
                "LO capped at 4. Per-faculty requirements available at ufh.ac.za. Seed based on institutional minimum from General Prospectus 2026.",
                ("English Home Language", 50, true, null));
        }

        if (added > 0)
        {
            await context.SaveChangesAsync();
            logger.LogInformation(
                "Pathways university expansion: added {Added} universities and {Courses} courses (Sprint 1.5.1 Gap 4).",
                added, coursesAdded);
        }
    }

    private static Career C(string name, string category, string description) =>
        new() { CareerId = Guid.NewGuid(), Name = name, Category = category, Description = description };

    private static University U(string name, string abbr, string province, string? website) =>
        new() { UniversityId = Guid.NewGuid(), Name = name, Abbreviation = abbr, Province = province, Website = website };
}
