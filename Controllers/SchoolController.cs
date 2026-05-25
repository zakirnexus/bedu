using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using SchoolProject.Data;
using SchoolProject.Models;
using SchoolProject.Models.ViewModels;
using SchoolProject.Services;

namespace SchoolProject.Controllers
{
    public class EnquiryDto
    {
        public string? Name { get; set; }
        public string? Email { get; set; }
        public string? Phone { get; set; }
        public string? Course { get; set; }
        public string? Message { get; set; }
        public int InstituteId { get; set; }
        public string? College { get; set; }
        public string? PageUrl { get; set; }
        public string? QueryType { get; set; }
        public string? Honeypot { get; set; }
        public string? RecaptchaToken { get; set; }
    }

    public class SchoolController : Controller
    {
        private readonly AppDbContext _context;
        private readonly SidebarService _sidebarService;
        private readonly ContentService _contentService;
        private readonly EmailService _emailService;
        private readonly ReCaptchaService _recaptchaService;
        private readonly IMemoryCache _cache;

        public SchoolController(
            AppDbContext context,
            SidebarService sidebarService,
            ContentService contentService,
            EmailService emailService,
            ReCaptchaService recaptchaService,
            IMemoryCache cache)
        {
            _context = context;
            _sidebarService = sidebarService;
            _contentService = contentService;
            _emailService = emailService;
            _recaptchaService = recaptchaService;
            _cache = cache;
        }

        private static int? ParseFeeMin(string? feesStructure)
        {
            if (string.IsNullOrWhiteSpace(feesStructure)) return null;

            var digits = new string(feesStructure.Where(char.IsDigit).ToArray());
            return digits.Length > 0 && int.TryParse(digits.Substring(0, Math.Min(digits.Length, 9)), out var val)
                ? val
                : (int?)null;
        }

        [HttpGet]
        [Route("{board}-schools-in-{city}")]
        [Route("{board}-schools-in-area/{nsewcSlug}-{city}")]
        public IActionResult List(
            string board,
            string city,
            string? nsewcSlug = null,
            int page = 1,
            string? locality = null,
            string? nsewc = null,
            int? coedId = null,
            int? ownershipId = null,
            string? feesRange = null)
        {
            if (Request.Path.Value?.EndsWith("/") == true)
            {
                var canonical = Request.Path.Value.TrimEnd('/');
                if (Request.QueryString.HasValue)
                    canonical += Request.QueryString.Value;
                return RedirectPermanent(canonical);
            }

            if (!string.IsNullOrWhiteSpace(nsewc) && int.TryParse(nsewc, out int nsewcRedirectId))
            {
                var nsewcRedirectSlug = _context.Nsewcs
                    .Where(n => n.NsewcId == nsewcRedirectId)
                    .Select(n => n.NsewcName)
                    .FirstOrDefault()?
                    .ToLower()
                    .Replace(" ", "-");

                if (!string.IsNullOrWhiteSpace(nsewcRedirectSlug))
                {
                    var cleanUrl = $"/{board}-schools-in-area/{nsewcRedirectSlug}-{city}";

                    var otherParams = new List<string>();
                    if (!string.IsNullOrWhiteSpace(locality)) otherParams.Add($"locality={locality}");
                    if (coedId.HasValue) otherParams.Add($"coedId={coedId}");
                    if (ownershipId.HasValue) otherParams.Add($"ownershipId={ownershipId}");
                    if (!string.IsNullOrWhiteSpace(feesRange)) otherParams.Add($"feesRange={Uri.EscapeDataString(feesRange)}");
                    if (page > 1) otherParams.Add($"page={page}");

                    if (otherParams.Any())
                        cleanUrl += "?" + string.Join("&", otherParams);

                    return RedirectPermanent(cleanUrl);
                }
            }

            int pageSize = 10;

            var syllabus = _context.Syllabuses
                .FirstOrDefault(s => s.SyllabusSlug != null &&
                                     s.SyllabusSlug.ToLower() == board.ToLower());

            if (syllabus == null)
                return Content("No syllabus found");

            city = city.Trim('/');

            var cityObj = _context.Cities
                .FirstOrDefault(c => c.CitySlug != null &&
                                     c.CitySlug.ToLower() == city.ToLower());

            if (cityObj == null)
            {
                var allSlugs = _context.Cities
                    .Where(c => c.CitySlug != null)
                    .Select(c => new { c.CityId, c.CityName, c.CitySlug })
                    .Take(30)
                    .ToList();

                return Content("No city found for: '" + city + "'\n\nAvailable slugs:\n" +
                    string.Join("\n", allSlugs.Select(x => $"id={x.CityId} | {x.CityName} | CitySlug={x.CitySlug}")));
            }

            if (!string.IsNullOrWhiteSpace(nsewcSlug) && string.IsNullOrWhiteSpace(nsewc))
            {
                var nsewcFromSlug = _context.Nsewcs
                    .FirstOrDefault(n => n.NsewcName != null &&
                                         n.NsewcName.ToLower().Replace(" ", "-") == nsewcSlug.ToLower());

                if (nsewcFromSlug != null)
                    nsewc = nsewcFromSlug.NsewcId.ToString();
            }

            var baseQuery = _context.Schools
                .Include(s => s.City)
                .Include(s => s.Ownership)
                .Include(s => s.Coed)
                .Include(s => s.Locality)
                .Include(s => s.NsewcNav)
                .Include(s => s.SchoolSyllabuses!)
                .Where(s =>
                    s.CityId == cityObj.CityId &&
                    s.SchoolSyllabuses!.Any(ss => ss.SyllabusId == syllabus.SyllabusId));

            ViewBag.Localities = _context.Localities
                .Where(l => l.CityId == cityObj.CityId &&
                            l.Schools!.Any(s => s.SchoolSyllabuses!.Any(ss => ss.SyllabusId == syllabus.SyllabusId)))
                .OrderBy(l => l.LocalityName)
                .Select(l => new { l.LocalityId, l.LocalityName })
                .ToList();

            ViewBag.NsewcOptions = _context.Nsewcs
                .Where(n => baseQuery.Any(s => s.NsewcId == n.NsewcId))
                .OrderBy(n => n.NsewcName)
                .Select(n => new { n.NsewcId, n.NsewcName })
                .ToList();

            ViewBag.CoedOptions = _context.Coeds
                .Where(c => baseQuery.Any(s => s.CoedId == c.CoedId))
                .OrderBy(c => c.CoedId)
                .Select(c => new { c.CoedId, c.CoedName })
                .ToList();

            ViewBag.OwnerOptions = baseQuery
                .Include(s => s.Ownership)
                .Where(s => s.Ownership != null && s.Ownership.InstOwnershipType != null)
                .Select(s => new { s.InstOwnershipId, Type = s.Ownership!.InstOwnershipType })
                .Distinct()
                .OrderBy(o => o.Type)
                .ToList();

            var query = baseQuery;

            if (!string.IsNullOrWhiteSpace(locality) && int.TryParse(locality, out int localityId))
                query = query.Where(s => s.LocalityId == localityId);

            if (!string.IsNullOrWhiteSpace(nsewc) && int.TryParse(nsewc, out int nsewcId))
                query = query.Where(s => s.NsewcId == nsewcId);

            if (coedId.HasValue)
                query = query.Where(s => s.CoedId == coedId.Value);

            if (ownershipId.HasValue)
                query = query.Where(s => s.InstOwnershipId == ownershipId.Value);

            var ordered = query
                .OrderBy(s =>
                    s.ListingRank == null || s.ListingRank == 0 ? 2 :
                    s.IsSponsored ? 1 : 0)
                .ThenBy(s => s.ListingRank == 0 || s.ListingRank == null ? s.InstituteName : null)
                .ThenBy(s => s.ListingRank);

            int totalRecords;
            List<School> schoolList;

            if (!string.IsNullOrWhiteSpace(feesRange))
            {
                var parts = feesRange.Split('-');
                int feeMin = parts.Length >= 1 && int.TryParse(parts[0], out var lo) ? lo : 0;
                int feeMax = parts.Length >= 2 && int.TryParse(parts[1], out var hi) ? hi : int.MaxValue;

                var allMatched = ordered
                    .ToList()
                    .Where(s =>
                    {
                        var fee = ParseFeeMin(s.FeesStructure);
                        return fee.HasValue && fee.Value >= feeMin && fee.Value <= feeMax;
                    })
                    .ToList();

                totalRecords = allMatched.Count;
                schoolList = allMatched.Skip((page - 1) * pageSize).Take(pageSize).ToList();
            }
            else
            {
                totalRecords = query.Count();
                schoolList = ordered.Skip((page - 1) * pageSize).Take(pageSize).ToList();
            }

            ViewBag.TotalRecords = totalRecords;
            ViewBag.TotalPages = (int)Math.Ceiling((double)totalRecords / pageSize);
            ViewBag.CurrentPage = page;
            ViewBag.Board = board;
            ViewBag.City = city;
            ViewBag.CitySlug = cityObj.CitySlug ?? city;
            ViewBag.CategorySlug = "schools";

            int? selLidFinal = (!string.IsNullOrWhiteSpace(locality) && int.TryParse(locality, out int selLid))
                ? selLid
                : (int?)null;

            ViewBag.SelLocality = selLidFinal;
            ViewBag.SelNsewc = nsewc;
            ViewBag.SelCoed = coedId;
            ViewBag.SelOwnership = ownershipId;
            ViewBag.SelFees = feesRange;
            ViewBag.FiltersActive = locality != null || nsewc != null || coedId != null || ownershipId != null || feesRange != null;

            ViewBag.SelNsewcName = (!string.IsNullOrWhiteSpace(nsewc) && int.TryParse(nsewc, out int snid))
                ? _context.Nsewcs.Where(n => n.NsewcId == snid).Select(n => n.NsewcName).FirstOrDefault()
                : null;

            ViewBag.SelLocalityName = selLidFinal.HasValue
                ? _context.Localities.Where(l => l.LocalityId == selLidFinal.Value).Select(l => l.LocalityName).FirstOrDefault()
                : null;

            ViewBag.SelCoedName = coedId.HasValue
                ? _context.Coeds.Where(c => c.CoedId == coedId.Value).Select(c => c.CoedName).FirstOrDefault()
                : null;

            ViewBag.SelOwnershipName = ownershipId.HasValue
                ? _context.InstOwnerships.Where(o => o.InstOwnershipId == ownershipId.Value).Select(o => o.InstOwnershipType).FirstOrDefault()
                : null;

            ViewBag.NsewcSlug = nsewcSlug;
            ViewBag.ShowFilterPanel = true;

            var titleParts = new List<string>();
            titleParts.Add(syllabus.SyllabusSlug!.ToUpper());
            if (ViewBag.SelNsewcName != null) titleParts.Add((string)ViewBag.SelNsewcName);
            titleParts.Add("Schools in");
            titleParts.Add(cityObj.CityName ?? city);

            ViewBag.Title = string.Join(" ", titleParts) + $" ({totalRecords} Schools)";
            if (page > 1) ViewBag.Title += $" - Page {page}";

            ViewBag.Description = $"Explore {totalRecords} {syllabus.SyllabusSlug!.ToUpper()} schools in {cityObj.CityName}. Compare fees, admission details, reviews and more.";
            if (page > 1) ViewBag.Description += $" Page {page} of results.";

            var seoContent = _context.SeoContents.FirstOrDefault(x =>
                x.CityId == cityObj.CityId &&
                x.SyllabusId == syllabus.SyllabusId &&
                x.PageType == "List" &&
                x.Section == "Top" &&
                x.IsActive == true);

            ViewBag.TopContent = seoContent?.Content;
            ViewBag.BottomContent = _contentService.GetContent("List", cityObj.CityId, syllabus.SyllabusId, "Bottom");
            ViewBag.Sidebar = _sidebarService.GetSchoolListSidebar(
                cityObj.CityId,
                syllabus.SyllabusId,
                cityObj.CityName ?? city,
                syllabus.SyllabusName ?? board,
                cityObj.CitySlug ?? city);

            return View("Index", schoolList);
        }

        [HttpGet]
        [Route("schools")]
        public IActionResult AllSchools(int page = 1)
        {
            int pageSize = 20;

            var query = _context.Schools
                .Include(s => s.City)
                .Include(s => s.Syllabus);

            int totalRecords = query.Count();

            var schools = query
                .OrderBy(s =>
                    s.ListingRank == null || s.ListingRank == 0 ? 2 :
                    s.IsSponsored ? 1 : 0)
                .ThenBy(s => s.ListingRank == 0 || s.ListingRank == null ? s.InstituteName : null)
                .ThenBy(s => s.ListingRank)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToList();

            ViewBag.TotalRecords = totalRecords;
            ViewBag.TotalPages = (int)Math.Ceiling((double)totalRecords / pageSize);
            ViewBag.CurrentPage = page;
            ViewBag.Title = $"All Schools in Bangalore ({totalRecords} Schools)";
            if (page > 1) ViewBag.Title += $" - Page {page}";
            ViewBag.Description = $"Browse all {totalRecords} schools in Bangalore. Filter by syllabus, area, fees and more.";

            var syllabusLinks = _context.Syllabuses
                .Where(s => s.SyllabusSlug != null)
                .ToList()
                .Select(s => new SidebarItem
                {
                    Title = $"{s.SyllabusName} Schools in Bangalore",
                    Url = $"/{s.SyllabusSlug}-schools-in-bangalore"
                })
                .ToList();

            var sidebar = new SidebarViewModel();
            sidebar.Sections.Add(new SidebarSection
            {
                Heading = "Browse by Syllabus",
                Items = syllabusLinks
            });

            ViewBag.Sidebar = sidebar;

            return View("Index", schools);
        }

        [HttpGet]
        [Route("schools-in-{city}")]
        public IActionResult SchoolsByCity(
            string city,
            int page = 1,
            string? locality = null,
            string? nsewc = null,
            int? coedId = null,
            int? ownershipId = null,
            string? feesRange = null,
            int? syllabusId = null)
        {
            if (Request.Path.Value?.EndsWith("/") == true)
            {
                var canonical = Request.Path.Value.TrimEnd('/');
                if (Request.QueryString.HasValue) canonical += Request.QueryString.Value;
                return RedirectPermanent(canonical);
            }

            int pageSize = 10;
            city = city.Trim('/');

            var cityObj = _context.Cities
                .FirstOrDefault(c => c.CitySlug != null && c.CitySlug.ToLower() == city.ToLower());

            if (cityObj == null)
                return Content("No city found for: '" + city + "'");

            var baseQuery = _context.Schools
                .Include(s => s.City)
                .Include(s => s.Ownership)
                .Include(s => s.Coed)
                .Include(s => s.Locality)
                .Include(s => s.NsewcNav)
                .Include(s => s.SchoolSyllabuses!)
                .Where(s => s.CityId == cityObj.CityId);

            var optionQuery = baseQuery;
            if (!string.IsNullOrWhiteSpace(nsewc) && int.TryParse(nsewc, out int nsewcOptId))
                optionQuery = optionQuery.Where(s => s.NsewcId == nsewcOptId);
            if (coedId.HasValue)
                optionQuery = optionQuery.Where(s => s.CoedId == coedId.Value);
            if (ownershipId.HasValue)
                optionQuery = optionQuery.Where(s => s.InstOwnershipId == ownershipId.Value);
            if (syllabusId.HasValue)
                optionQuery = optionQuery.Where(s => s.SchoolSyllabuses!.Any(ss => ss.SyllabusId == syllabusId.Value));

            ViewBag.Localities = _context.Localities
                .Where(l => l.CityId == cityObj.CityId && optionQuery.Any(s => s.LocalityId == l.LocalityId))
                .OrderBy(l => l.LocalityName)
                .Select(l => new { l.LocalityId, l.LocalityName })
                .ToList();

            var localityScopedQuery = baseQuery;
            if (!string.IsNullOrWhiteSpace(locality) && int.TryParse(locality, out int localityOptId))
                localityScopedQuery = localityScopedQuery.Where(s => s.LocalityId == localityOptId);
            if (coedId.HasValue)
                localityScopedQuery = localityScopedQuery.Where(s => s.CoedId == coedId.Value);
            if (ownershipId.HasValue)
                localityScopedQuery = localityScopedQuery.Where(s => s.InstOwnershipId == ownershipId.Value);
            if (syllabusId.HasValue)
                localityScopedQuery = localityScopedQuery.Where(s => s.SchoolSyllabuses!.Any(ss => ss.SyllabusId == syllabusId.Value));

            ViewBag.NsewcOptions = _context.Nsewcs
                .Where(n => localityScopedQuery.Any(s => s.NsewcId == n.NsewcId))
                .OrderBy(n => n.NsewcName)
                .Select(n => new { n.NsewcId, n.NsewcName })
                .ToList();

            ViewBag.CoedOptions = _context.Coeds
                .Where(c => optionQuery.Any(s => s.CoedId == c.CoedId))
                .OrderBy(c => c.CoedId)
                .Select(c => new { c.CoedId, c.CoedName })
                .ToList();

            ViewBag.OwnerOptions = optionQuery
                .Include(s => s.Ownership)
                .Where(s => s.Ownership != null && s.Ownership.InstOwnershipType != null)
                .Select(s => new { s.InstOwnershipId, Type = s.Ownership!.InstOwnershipType })
                .Distinct()
                .OrderBy(o => o.Type)
                .ToList();

            ViewBag.SyllabusOptions = _context.Syllabuses
                .Where(sy => optionQuery.Any(s => s.SchoolSyllabuses!.Any(ss => ss.SyllabusId == sy.SyllabusId)))
                .OrderBy(sy => sy.SyllabusName)
                .Select(sy => new { sy.SyllabusId, sy.SyllabusName })
                .ToList();

            var query = baseQuery;
            if (!string.IsNullOrWhiteSpace(locality) && int.TryParse(locality, out int localityId))
                query = query.Where(s => s.LocalityId == localityId);
            if (!string.IsNullOrWhiteSpace(nsewc) && int.TryParse(nsewc, out int nsewcIdCity))
                query = query.Where(s => s.NsewcId == nsewcIdCity);
            if (coedId.HasValue)
                query = query.Where(s => s.CoedId == coedId.Value);
            if (ownershipId.HasValue)
                query = query.Where(s => s.InstOwnershipId == ownershipId.Value);
            if (syllabusId.HasValue)
                query = query.Where(s => s.SchoolSyllabuses!.Any(ss => ss.SyllabusId == syllabusId.Value));

            var ordered = query
                .OrderBy(s => s.ListingRank == null || s.ListingRank == 0 ? 2 : s.IsSponsored ? 1 : 0)
                .ThenBy(s => s.ListingRank == 0 || s.ListingRank == null ? s.InstituteName : null)
                .ThenBy(s => s.ListingRank);

            int totalRecords;
            List<School> schoolList;

            if (!string.IsNullOrWhiteSpace(feesRange))
            {
                var parts = feesRange.Split('-');
                int feeMin = parts.Length >= 1 && int.TryParse(parts[0], out var lo) ? lo : 0;
                int feeMax = parts.Length >= 2 && int.TryParse(parts[1], out var hi) ? hi : int.MaxValue;

                var allMatched = ordered.ToList()
                    .Where(s =>
                    {
                        var fee = ParseFeeMin(s.FeesStructure);
                        return fee.HasValue && fee.Value >= feeMin && fee.Value <= feeMax;
                    })
                    .ToList();

                totalRecords = allMatched.Count;
                schoolList = allMatched.Skip((page - 1) * pageSize).Take(pageSize).ToList();
            }
            else
            {
                totalRecords = query.Count();
                schoolList = ordered.Skip((page - 1) * pageSize).Take(pageSize).ToList();
            }

            ViewBag.TotalRecords = totalRecords;
            ViewBag.TotalPages = (int)Math.Ceiling((double)totalRecords / pageSize);
            ViewBag.CurrentPage = page;
            ViewBag.City = city;
            ViewBag.Board = null;
            ViewBag.CitySlug = cityObj.CitySlug ?? city;
            ViewBag.CategorySlug = "schools";

            int? selLidCity = (!string.IsNullOrWhiteSpace(locality) && int.TryParse(locality, out int sl)) ? sl : (int?)null;
            ViewBag.SelLocality = selLidCity;
            ViewBag.SelNsewc = nsewc;
            ViewBag.SelCoed = coedId;
            ViewBag.SelOwnership = ownershipId;
            ViewBag.SelFees = feesRange;
            ViewBag.SelSyllabus = syllabusId;
            ViewBag.FiltersActive = locality != null || nsewc != null || coedId != null || ownershipId != null || feesRange != null || syllabusId != null;

            ViewBag.SelNsewcName = (!string.IsNullOrWhiteSpace(nsewc) && int.TryParse(nsewc, out int snid))
                ? _context.Nsewcs.Where(n => n.NsewcId == snid).Select(n => n.NsewcName).FirstOrDefault()
                : null;

            ViewBag.SelLocalityName = selLidCity.HasValue
                ? _context.Localities.Where(l => l.LocalityId == selLidCity.Value).Select(l => l.LocalityName).FirstOrDefault()
                : null;

            ViewBag.SelCoedName = coedId.HasValue
                ? _context.Coeds.Where(c => c.CoedId == coedId.Value).Select(c => c.CoedName).FirstOrDefault()
                : null;

            ViewBag.SelOwnershipName = ownershipId.HasValue
                ? _context.InstOwnerships.Where(o => o.InstOwnershipId == ownershipId.Value).Select(o => o.InstOwnershipType).FirstOrDefault()
                : null;

            ViewBag.SelSyllabusName = syllabusId.HasValue
                ? _context.Syllabuses.Where(s => s.SyllabusId == syllabusId.Value).Select(s => s.SyllabusName).FirstOrDefault()
                : null;

            ViewBag.NsewcSlug = null;
            ViewBag.ShowFilterPanel = true;

            var cityTitleParts = new List<string>();
            if (ViewBag.SelSyllabusName != null) cityTitleParts.Add(((string)ViewBag.SelSyllabusName).ToUpper());
            if (ViewBag.SelNsewcName != null) cityTitleParts.Add((string)ViewBag.SelNsewcName);
            if (ViewBag.SelCoedName != null) cityTitleParts.Add((string)ViewBag.SelCoedName);
            if (ViewBag.SelOwnershipName != null) cityTitleParts.Add((string)ViewBag.SelOwnershipName);
            cityTitleParts.Add("Schools in");
            cityTitleParts.Add(cityObj.CityName ?? city);

            ViewBag.Title = string.Join(" ", cityTitleParts) + $" ({totalRecords} Schools)";
            if (page > 1) ViewBag.Title += $" - Page {page}";

            ViewBag.Description = $"Browse {totalRecords} schools in {cityObj.CityName}. Filter by locality, syllabus, fees and more.";
            if (page > 1) ViewBag.Description += $" Page {page} of results.";

            var syllabusLinks = _context.Syllabuses
                .Where(s => s.SyllabusSlug != null)
                .ToList()
                .Select(s => new SidebarItem
                {
                    Title = $"{s.SyllabusName} Schools in {cityObj.CityName}",
                    Url = $"/{s.SyllabusSlug}-schools-in-{cityObj.CitySlug}"
                })
                .ToList();

            var sidebar = new SidebarViewModel();
            sidebar.Sections.Add(new SidebarSection
            {
                Heading = "Browse by Syllabus",
                Items = syllabusLinks
            });

            ViewBag.Sidebar = sidebar;

            return View("Index", schoolList);
        }

        [HttpGet("school/{slug}")]
        public IActionResult Details(string slug)
        {
            var school = _context.Schools
                .Include(s => s.City)
                .Include(s => s.Syllabus)
                .Include(s => s.SchoolSyllabuses!)
                    .ThenInclude(ss => ss.Syllabus)
                .FirstOrDefault(s => s.InstituteSlug == slug);

            if (school == null)
                return Content("Slug not found: " + slug);

            ViewBag.Sidebar = _sidebarService.GetSchoolSidebar(school);
            ViewBag.DetailContent = _contentService.GetContent("Details", school.CityId, school.SyllabusId, "Bottom");
            ViewBag.Title = $"{school.InstituteName} in {school.City?.CityName}";
            ViewBag.Description = $"{school.InstituteName} is a {school.Syllabus?.SyllabusName} school located in {school.City?.CityName}.";
            ViewBag.Canonical = $"/school/{school.InstituteSlug}";
            ViewBag.InstituteId = school.InstituteId;
            ViewBag.InstituteName = school.InstituteName;
            ViewBag.Photos = school.Photos;

            return View("Details", school);
        }

        [HttpPost]
        [Route("Enquiry/Submit")]
        public async Task<IActionResult> Submit([FromBody] EnquiryDto dto)
        {
            if (dto == null)
                return BadRequest(new { success = false, message = "No data received" });

            if (!string.IsNullOrWhiteSpace(dto.Honeypot))
            {
                Console.WriteLine("Honeypot triggered");
                return Ok(new { success = true });
            }

            var ip = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
            var rateLimitKey = $"enquiry_rate_{ip}";
            var hourKey = $"enquiry_hour_{ip}";

            int minuteCount = _cache.GetOrCreate(rateLimitKey, e =>
            {
                e.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(1);
                return 0;
            });

            int hourCount = _cache.GetOrCreate(hourKey, e =>
            {
                e.AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(1);
                return 0;
            });

            if (minuteCount >= 3 || hourCount >= 10)
                return StatusCode(429, new { success = false, message = "Too many submissions. Please try again later." });

            _cache.Set(rateLimitKey, minuteCount + 1, TimeSpan.FromMinutes(1));
            _cache.Set(hourKey, hourCount + 1, TimeSpan.FromHours(1));

            if (!string.IsNullOrWhiteSpace(dto.RecaptchaToken))
            {
                bool isHuman = await _recaptchaService.IsValidAsync(dto.RecaptchaToken, ip);
                if (!isHuman)
                {
                    Console.WriteLine("reCAPTCHA failed");
                    return BadRequest(new { success = false, message = "Verification failed. Please try again." });
                }
            }

            var dupKey = $"dup_{dto.Email}_{dto.InstituteId}";
            if (_cache.TryGetValue(dupKey, out _))
                return Ok(new { success = true });

            _cache.Set(dupKey, true, TimeSpan.FromMinutes(5));

            var enquiry = new Enquiry
            {
                Name = dto.Name,
                Email = dto.Email,
                Phone = dto.Phone,
                Course = dto.Course,
                Message = dto.Message,
                College = dto.College,
                InstituteId = dto.InstituteId,
                PageUrl = dto.PageUrl,
                QueryType = dto.QueryType,
                EntryDate = DateTime.Now,
                ClassFn = "School Enquiry"
            };

            try
            {
                _context.Enquiries.Add(enquiry);
                _context.SaveChanges();
            }
            catch (Exception saveEx)
            {
                Console.WriteLine("Enquiry save failed: " + saveEx.Message);
            }

            string? phone = null;
            string? schoolEmail = null;

            if (dto.InstituteId > 0)
            {
                var school = _context.Schools.FirstOrDefault(s => s.InstituteId == dto.InstituteId);
                phone = school?.Telephone;
                schoolEmail = school?.Email;
            }

            try
            {
                if (!string.IsNullOrWhiteSpace(schoolEmail))
                {
                    _emailService.SendEnquiryEmail(
                        toEmail: schoolEmail,
                        instituteName: dto.College ?? "",
                        fromName: dto.Name ?? "",
                        fromEmail: dto.Email ?? "",
                        fromPhone: dto.Phone ?? "",
                        course: dto.Course ?? "",
                        message: dto.Message ?? "",
                        queryType: dto.QueryType ?? "Enquiry",
                        pageUrl: dto.PageUrl ?? ""
                    );
                }
            }
            catch (Exception emailEx)
            {
                Console.WriteLine("Email send failed: " + emailEx.Message);
                Console.WriteLine("Email stack: " + emailEx.StackTrace);
            }

            return Ok(new { success = true, phone = phone });
        }
    }
}