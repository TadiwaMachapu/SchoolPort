using Microsoft.EntityFrameworkCore;
using SchoolPortal.Data;
using SchoolPortal.Data.Entities;

namespace SchoolPortal.Server.Seeds;

public static class MatricHubSeedData
{
    // DBE past papers portal — stable landing page for all NSC papers
    private const string DbeUrl = "https://www.education.gov.za/Curriculum/NationalSenioCertificate(NSC)Examinations/NSCPreviousExaminationPapers.aspx";

    public static async Task SeedAsync(SchoolPortalDbContext context, ILogger logger)
    {
        await SeedPastPapersAsync(context, logger);
        await SeedQuizQuestionsAsync(context, logger);
        await SeedExtendedSeniorPhaseRequirementsAsync(context, logger);
        await context.SaveChangesAsync();

        // Sprint 1.5.2: verified-URL past-paper catalogue (additive upsert; also heals the
        // broken v1 index URL and deactivates never-published phantom rows). Runs every start.
        await MatricPastPaperSeedData.SyncAsync(context, logger);
    }

    // ── Past Papers ──────────────────────────────────────────────────────────────

    private static async Task SeedPastPapersAsync(SchoolPortalDbContext context, ILogger logger)
    {
        if (await context.MatricPastPapers.AnyAsync()) return;

        logger.LogInformation("Seeding Matric past papers…");

        var papers = new List<MatricPastPaper>();

        void Add(string subject, int year, int paper, bool hasMemo = true, string? notes = null) =>
            papers.Add(new MatricPastPaper
            {
                MatricPastPaperId = Guid.NewGuid(),
                Subject = subject,
                Year = year,
                PaperNumber = paper,
                Language = "English",
                Url = DbeUrl,
                HasMemo = hasMemo,
                MemoUrl = hasMemo ? DbeUrl : null,
                Notes = notes ?? $"{subject} — November {year} Paper {paper}"
            });

        var subjects1Paper = new[] { "English Home Language", "Afrikaans Home Language" };
        var subjects2Papers = new[]
        {
            "Mathematics", "Mathematical Literacy", "Physical Sciences", "Life Sciences",
            "Geography", "History", "Accounting", "Business Studies", "Economics",
            "Computer Applications Technology", "Information Technology"
        };
        var years = new[] { 2019, 2020, 2021, 2022, 2023 };

        foreach (var year in years)
        {
            foreach (var subj in subjects1Paper)
                Add(subj, year, 1);

            foreach (var subj in subjects2Papers)
            {
                Add(subj, year, 1);
                Add(subj, year, 2);
            }
        }

        context.MatricPastPapers.AddRange(papers);
        logger.LogInformation("Seeded {Count} past paper records", papers.Count);
    }

    // ── Quiz Questions ───────────────────────────────────────────────────────────

    private static async Task SeedQuizQuestionsAsync(SchoolPortalDbContext context, ILogger logger)
    {
        if (await context.MatricQuizQuestions.AnyAsync()) return;

        logger.LogInformation("Seeding Matric quiz questions…");

        var q = new List<MatricQuizQuestion>();

        void Q(string subj, string diff, string text, string a, string b, string c, string d, string correct, string? explain = null) =>
            q.Add(new MatricQuizQuestion
            {
                MatricQuizQuestionId = Guid.NewGuid(),
                Subject = subj,
                Difficulty = diff,
                QuestionText = text,
                OptionA = a, OptionB = b, OptionC = c, OptionD = d,
                CorrectOption = correct,
                Explanation = explain
            });

        // ── Mathematics ──────────────────────────────────────────────────────────
        Q("Mathematics", "Easy",
            "Solve for x: 2x + 6 = 14",
            "x = 3", "x = 4", "x = 5", "x = 10",
            "B", "2x = 14 − 6 = 8, so x = 4.");

        Q("Mathematics", "Medium",
            "The roots of x² − 5x + 6 = 0 are:",
            "x = 1 and x = 6", "x = 2 and x = 3", "x = −2 and x = −3", "x = 3 and x = 6",
            "B", "Factorise: (x − 2)(x − 3) = 0, so x = 2 or x = 3.");

        Q("Mathematics", "Medium",
            "If f(x) = 3x² − 12x + 9, the x-value at the turning point is:",
            "x = 1", "x = 2", "x = 3", "x = 4",
            "B", "x = −b/2a = 12/6 = 2.");

        Q("Mathematics", "Hard",
            "The sum of the first n terms of an arithmetic series is Sₙ = n/2(2a + (n−1)d). If a = 3, d = 4 and Sₙ = 120, find n.",
            "n = 8", "n = 10", "n = 12", "n = 15",
            "B", "120 = n/2(6 + 4(n−1)) → 240 = n(4n+2) → 4n² + 2n − 240 = 0 → n = 10.");

        Q("Mathematics", "Easy",
            "What is the gradient of the line 3y = 6x − 9?",
            "m = 3", "m = 2", "m = −3", "m = 6",
            "B", "Divide by 3: y = 2x − 3, so gradient = 2.");

        Q("Mathematics", "Medium",
            "Simplify: (x² − 4)/(x − 2)",
            "x − 2", "x + 2", "x² + 2", "x² − 2",
            "B", "(x−2)(x+2)/(x−2) = x + 2 (where x ≠ 2).");

        Q("Mathematics", "Hard",
            "A circle has equation x² + y² − 6x + 4y − 3 = 0. Its radius is:",
            "r = 3", "r = 4", "r = √22", "r = 5",
            "B", "Complete the square: (x−3)² + (y+2)² = 9 + 4 + 3 = 16, so r = √16 = 4.");

        Q("Mathematics", "Easy",
            "log₂(8) equals:",
            "2", "3", "4", "8",
            "B", "2³ = 8, so log₂(8) = 3.");

        // ── Physical Sciences ─────────────────────────────────────────────────────
        Q("Physical Sciences", "Easy",
            "Which of the following is the SI unit of force?",
            "Joule", "Watt", "Newton", "Pascal",
            "C", "Force is measured in Newtons (N). F = ma.");

        Q("Physical Sciences", "Medium",
            "A car of mass 1 000 kg accelerates at 2 m·s⁻². The net force acting on it is:",
            "500 N", "1 000 N", "2 000 N", "5 000 N",
            "C", "F = ma = 1000 × 2 = 2 000 N.");

        Q("Physical Sciences", "Medium",
            "The wavelength of a wave is 0.5 m and its frequency is 200 Hz. Its speed is:",
            "100 m·s⁻¹", "400 m·s⁻¹", "0.0025 m·s⁻¹", "200 m·s⁻¹",
            "A", "v = fλ = 200 × 0.5 = 100 m·s⁻¹.");

        Q("Physical Sciences", "Hard",
            "Two charges of +3 μC and −3 μC are placed 0.3 m apart. The magnitude of the electrostatic force between them (k = 9×10⁹ N·m²·C⁻²) is:",
            "0.9 N", "9 N", "0.3 N", "3 N",
            "A", "F = kq₁q₂/r² = 9×10⁹ × 3×10⁻⁶ × 3×10⁻⁶ / 0.09 = 9×10⁹ × 9×10⁻¹² / 0.09 = 0.9 N.");

        Q("Physical Sciences", "Easy",
            "Which particle carries a negative charge?",
            "Proton", "Neutron", "Electron", "Nucleus",
            "C", "Electrons carry a negative charge of −1.6×10⁻¹⁹ C.");

        Q("Physical Sciences", "Medium",
            "In which type of reaction is energy released to the surroundings?",
            "Endothermic", "Exothermic", "Neutral", "Catalytic",
            "B", "Exothermic reactions release energy (heat) to the surroundings, e.g., combustion.");

        Q("Physical Sciences", "Hard",
            "The half-life of a radioactive substance is 30 years. After 90 years, what fraction of the original sample remains?",
            "1/2", "1/4", "1/8", "1/16",
            "C", "90 years = 3 half-lives. Fraction remaining = (1/2)³ = 1/8.");

        Q("Physical Sciences", "Easy",
            "Ohm's Law states that V = IR. If V = 12 V and R = 4 Ω, the current I is:",
            "3 A", "48 A", "0.33 A", "8 A",
            "A", "I = V/R = 12/4 = 3 A.");

        // ── Life Sciences ─────────────────────────────────────────────────────────
        Q("Life Sciences", "Easy",
            "Which organelle is responsible for producing energy (ATP) in the cell?",
            "Nucleus", "Ribosome", "Mitochondrion", "Vacuole",
            "C", "Mitochondria carry out cellular respiration to produce ATP.");

        Q("Life Sciences", "Medium",
            "In DNA, adenine pairs with:",
            "Cytosine", "Guanine", "Thymine", "Uracil",
            "C", "In DNA, A pairs with T (adenine–thymine). In RNA, A pairs with U.");

        Q("Life Sciences", "Medium",
            "Which blood type is known as the universal donor?",
            "A", "B", "AB", "O",
            "D", "Blood type O negative has no A, B, or Rh antigens, making it the universal donor.");

        Q("Life Sciences", "Hard",
            "During meiosis, crossing over occurs in:",
            "Prophase I", "Metaphase II", "Anaphase I", "Telophase II",
            "A", "Crossing over (recombination) occurs during Prophase I when homologous chromosomes pair up.");

        Q("Life Sciences", "Easy",
            "Photosynthesis takes place in which organelle?",
            "Mitochondrion", "Chloroplast", "Ribosome", "Nucleus",
            "B", "Chloroplasts contain chlorophyll and are the site of photosynthesis.");

        Q("Life Sciences", "Medium",
            "Which hormone controls the blood glucose level by stimulating uptake of glucose into cells?",
            "Glucagon", "Adrenalin", "Insulin", "Oestrogen",
            "C", "Insulin (produced by the pancreas) lowers blood glucose by promoting cellular uptake.");

        Q("Life Sciences", "Hard",
            "The theory of natural selection was proposed by:",
            "Gregor Mendel", "Louis Pasteur", "Charles Darwin", "Watson and Crick",
            "C", "Charles Darwin (and Alfred Russel Wallace) proposed natural selection in 1858.");

        Q("Life Sciences", "Medium",
            "Which phase of the cell cycle involves DNA replication?",
            "G1 phase", "S phase", "G2 phase", "M phase",
            "B", "DNA replication occurs during the S (Synthesis) phase of interphase.");

        // ── Accounting ───────────────────────────────────────────────────────────
        Q("Accounting", "Easy",
            "The accounting equation is:",
            "Assets = Liabilities + Income", "Assets = Liabilities + Owner's Equity",
            "Liabilities = Assets + Owner's Equity", "Owner's Equity = Assets + Liabilities",
            "B", "The fundamental accounting equation: Assets = Liabilities + Owner's Equity.");

        Q("Accounting", "Medium",
            "Which of the following is a current asset?",
            "Land", "Buildings", "Trade debtors", "Long-term loans",
            "C", "Trade debtors (accounts receivable) are current assets — expected to be converted to cash within a year.");

        Q("Accounting", "Medium",
            "Gross profit is calculated as:",
            "Net sales − Operating expenses", "Net sales − Cost of sales",
            "Cost of sales − Operating expenses", "Net sales + Cost of sales",
            "B", "Gross profit = Net sales − Cost of sales (before deducting operating expenses).");

        Q("Accounting", "Hard",
            "A business has a current ratio of 2:1, current assets of R80 000. Its current liabilities are:",
            "R160 000", "R40 000", "R20 000", "R80 000",
            "B", "Current ratio = Current assets / Current liabilities → 2 = 80 000 / CL → CL = R40 000.");

        Q("Accounting", "Easy",
            "Depreciation is recorded in which financial statement?",
            "Balance Sheet only", "Income Statement only",
            "Both Income Statement and Balance Sheet", "Cash Flow Statement only",
            "C", "Depreciation appears in the Income Statement (as an expense) and reduces the asset value on the Balance Sheet.");

        Q("Accounting", "Medium",
            "Which bookkeeping principle states that every transaction has a dual effect?",
            "Going concern", "Historical cost", "Double entry", "Consistency",
            "C", "The double entry principle: every transaction affects at least two accounts (debit and credit).");

        Q("Accounting", "Hard",
            "Stock turnover rate = Cost of sales / Average stock. If cost of sales = R300 000 and opening stock = R40 000, closing stock = R60 000, stock turnover is:",
            "5 times", "6 times", "7.5 times", "10 times",
            "B", "Average stock = (40 000 + 60 000)/2 = 50 000. Turnover = 300 000 / 50 000 = 6 times.");

        Q("Accounting", "Easy",
            "VAT currently levied in South Africa is:",
            "10%", "12%", "14%", "15%",
            "D", "South Africa's standard VAT rate is 15% (increased from 14% in April 2018).");

        // ── Business Studies ──────────────────────────────────────────────────────
        Q("Business Studies", "Easy",
            "Which form of business ownership offers limited liability to its owners?",
            "Sole trader", "Partnership", "Private company (Pty) Ltd", "Close corporation",
            "C", "A private company (Pty Ltd) offers limited liability — shareholders' personal assets are protected.");

        Q("Business Studies", "Medium",
            "SWOT analysis stands for:",
            "Strengths, Weaknesses, Objectives, Threats",
            "Strengths, Weaknesses, Opportunities, Threats",
            "Skills, Workforce, Opportunities, Time",
            "Strategy, Workforce, Operations, Threats",
            "B", "SWOT: Strengths and Weaknesses (internal), Opportunities and Threats (external).");

        Q("Business Studies", "Medium",
            "The Consumer Protection Act in South Africa is Act:",
            "No. 68 of 2008", "No. 89 of 1998", "No. 55 of 2003", "No. 71 of 2008",
            "A", "The Consumer Protection Act 68 of 2008 protects consumer rights in South Africa.");

        Q("Business Studies", "Easy",
            "Which management function involves setting goals and deciding how to achieve them?",
            "Organising", "Leading", "Planning", "Controlling",
            "C", "Planning is the first management function: setting objectives and determining how to achieve them.");

        Q("Business Studies", "Hard",
            "A business charges R500 per unit variable cost and has fixed costs of R200 000. If selling price is R700, the break-even point in units is:",
            "400 units", "1 000 units", "2 000 units", "286 units",
            "B", "Contribution per unit = 700 − 500 = R200. Break-even = 200 000 / 200 = 1 000 units.");

        Q("Business Studies", "Medium",
            "Which legislation in South Africa governs employment equity?",
            "Labour Relations Act", "Basic Conditions of Employment Act",
            "Employment Equity Act", "Skills Development Act",
            "C", "The Employment Equity Act 55 of 1998 promotes equity and prevents unfair discrimination in the workplace.");

        Q("Business Studies", "Easy",
            "Total revenue is calculated as:",
            "Selling price × Variable cost", "Selling price × Number of units sold",
            "Fixed cost + Variable cost", "Net profit + Total expenses",
            "B", "Total Revenue = Price × Quantity sold.");

        Q("Business Studies", "Hard",
            "Which type of merger involves companies at different stages of the same production process?",
            "Horizontal merger", "Vertical merger", "Conglomerate merger", "Lateral merger",
            "B", "A vertical merger combines companies at different stages of the supply chain (e.g., a car manufacturer merging with a tyre supplier).");

        // ── History ──────────────────────────────────────────────────────────────
        Q("History", "Easy",
            "In which year did South Africa hold its first democratic elections?",
            "1990", "1992", "1994", "1996",
            "C", "South Africa's first fully democratic elections were held on 27 April 1994.");

        Q("History", "Medium",
            "The Sharpeville Massacre occurred in:",
            "1956", "1960", "1964", "1976",
            "B", "The Sharpeville Massacre took place on 21 March 1960 when police shot protesters against pass laws.");

        Q("History", "Medium",
            "The Cold War was primarily a conflict between:",
            "USA and China", "USA and USSR", "UK and Germany", "USA and Cuba",
            "B", "The Cold War (1947–1991) was the geopolitical tension between the USA and the USSR.");

        Q("History", "Easy",
            "Who was the first president of post-apartheid South Africa?",
            "F.W. de Klerk", "Walter Sisulu", "Nelson Mandela", "Thabo Mbeki",
            "C", "Nelson Mandela became South Africa's first democratically elected president in May 1994.");

        Q("History", "Hard",
            "The Treason Trial of 1956–1961 resulted in:",
            "Conviction of all accused", "Acquittal of all accused",
            "Life sentences for the accused", "The banning of the ANC",
            "B", "All 156 accused in the Treason Trial were acquitted by 1961.");

        Q("History", "Medium",
            "The Marshall Plan (1948) was a US programme to:",
            "Rebuild Western Europe after WWII", "Contain communism in Asia",
            "Establish NATO", "Fund the Berlin Airlift",
            "A", "The Marshall Plan provided over $13 billion to rebuild Western European economies devastated by WWII.");

        Q("History", "Hard",
            "The Freedom Charter was adopted at the Congress of the People in:",
            "Johannesburg, 1954", "Kliptown, 1955", "Cape Town, 1956", "Durban, 1957",
            "B", "The Freedom Charter was adopted at Kliptown, Soweto on 26 June 1955.");

        Q("History", "Easy",
            "Apartheid means, in Afrikaans:",
            "Freedom", "Equality", "Separateness", "Unity",
            "C", "Apartheid is an Afrikaans word meaning 'separateness' or 'apartness'.");

        // ── Geography ────────────────────────────────────────────────────────────
        Q("Geography", "Easy",
            "Which layer of the atmosphere contains the ozone layer?",
            "Troposphere", "Stratosphere", "Mesosphere", "Thermosphere",
            "B", "The ozone layer is located in the stratosphere, approximately 15–35 km above Earth's surface.");

        Q("Geography", "Medium",
            "The process by which water moves from soil/plants into the atmosphere is called:",
            "Condensation", "Precipitation", "Evapotranspiration", "Infiltration",
            "C", "Evapotranspiration combines evaporation from surfaces and transpiration from plants.");

        Q("Geography", "Medium",
            "South Africa's Drakensberg Mountains were formed primarily by:",
            "Folding", "Volcanic activity", "Erosion of a plateau", "Faulting",
            "C", "The Drakensberg escarpment formed through differential erosion of the ancient African Plateau.");

        Q("Geography", "Easy",
            "Which ocean borders South Africa's west coast?",
            "Indian Ocean", "Atlantic Ocean", "Pacific Ocean", "Southern Ocean",
            "B", "The Atlantic Ocean borders South Africa's west coast; the Indian Ocean borders the east coast.");

        Q("Geography", "Hard",
            "A city's CBD (Central Business District) is characterised by:",
            "Low land values and low-rise buildings",
            "High land values, high-rise buildings, and high accessibility",
            "Residential land use predominantly",
            "Industrial land use predominantly",
            "B", "The CBD has the highest land values in a city, tallest buildings, and greatest accessibility due to transport convergence.");

        Q("Geography", "Medium",
            "The Coriolis effect causes winds in the Southern Hemisphere to deflect:",
            "To the right", "To the left", "Upward", "Downward",
            "B", "In the Southern Hemisphere, the Coriolis effect deflects moving objects (including wind) to the left.");

        Q("Geography", "Hard",
            "Which type of rainfall occurs when moist air is forced to rise over a mountain range?",
            "Convectional rainfall", "Frontal rainfall", "Orographic rainfall", "Cyclonic rainfall",
            "C", "Orographic (relief) rainfall occurs when moist air is forced upward by a mountain barrier, cools, and condenses.");

        Q("Geography", "Easy",
            "The Sahara Desert is located on which continent?",
            "Asia", "Australia", "South America", "Africa",
            "D", "The Sahara Desert is the world's largest hot desert, located in northern Africa.");

        context.MatricQuizQuestions.AddRange(q);
        logger.LogInformation("Seeded {Count} quiz questions", q.Count);
    }

    // ── Extended SeniorPhaseRequirements ─────────────────────────────────────────

    private static async Task SeedExtendedSeniorPhaseRequirementsAsync(SchoolPortalDbContext context, ILogger logger)
    {
        var existing = await context.SeniorPhaseRequirements
            .Select(r => r.FetSubjectName)
            .ToListAsync();

        var toAdd = new List<SeniorPhaseRequirement>();

        void AddIfMissing(string fetSubject, string gr9Subject, int recommendedMin, string notes)
        {
            if (!existing.Contains(fetSubject))
                toAdd.Add(new SeniorPhaseRequirement
                {
                    SeniorPhaseRequirementId = Guid.NewGuid(),
                    FetSubjectName = fetSubject,
                    RequiredSeniorPhaseSubjectName = gr9Subject,
                    RecommendedMinPercent = recommendedMin,
                    Notes = notes
                });
        }

        AddIfMissing("Business Studies",            "Economic and Management Sciences", 40, "EMS in Gr 7–9 introduces basic business concepts.");
        AddIfMissing("Economics",                   "Economic and Management Sciences", 40, "EMS covers introductory economic concepts.");
        AddIfMissing("Tourism",                     "Social Sciences",                  40, "Geography component of Social Sciences is relevant.");
        AddIfMissing("Information Technology",      "Mathematics",                      50, "Strong Maths foundation is essential for IT programming.");
        AddIfMissing("Computer Applications Technology", "Mathematics",                 40, "Basic Maths skills support CAT spreadsheet and database work.");
        AddIfMissing("Engineering Graphics & Design", "Mathematics",                    50, "Spatial reasoning developed through Maths is essential for EGD.");
        AddIfMissing("Agricultural Sciences",       "Natural Sciences",                 40, "Biology component of Natural Sciences supports Agricultural Sciences.");
        AddIfMissing("Consumer Studies",            "Technology",                       40, "Technology subject in Gr 7–9 introduces design and processing concepts.");

        if (toAdd.Any())
        {
            context.SeniorPhaseRequirements.AddRange(toAdd);
            logger.LogInformation("Added {Count} extended SeniorPhaseRequirements", toAdd.Count);
        }
    }
}
