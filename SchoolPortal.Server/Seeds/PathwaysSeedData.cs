using Microsoft.EntityFrameworkCore;
using SchoolPortal.Data;
using SchoolPortal.Data.Entities;

namespace SchoolPortal.Server.Seeds;

public static class PathwaysSeedData
{
    public static async Task SeedAsync(SchoolPortalDbContext context, ILogger logger)
    {
        if (await context.Universities.AnyAsync()) return;

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
    }

    private static Career C(string name, string category, string description) =>
        new() { CareerId = Guid.NewGuid(), Name = name, Category = category, Description = description };

    private static University U(string name, string abbr, string province, string? website) =>
        new() { UniversityId = Guid.NewGuid(), Name = name, Abbreviation = abbr, Province = province, Website = website };
}
