using Elastic.Clients.Elasticsearch;
using Elastic.Clients.Elasticsearch.QueryDsl;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SchoolProject.Data;
using SchoolProject.Models;
using SchoolProject.Models.Search;

namespace SchoolProject.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class SchoolSearchController : ControllerBase
    {
        private readonly ElasticsearchClient _client;
        private readonly AppDbContext _context;

        public SchoolSearchController(ElasticsearchClient client, AppDbContext context)
        {
            _client = client;
            _context = context;
        }

        [HttpGet]
        public async Task<IActionResult> Get([FromQuery] SchoolSearchRequest request)
        {
            var page = request.Page <= 0 ? 1 : request.Page;
            var pageSize = request.PageSize <= 0 ? 10 : Math.Min(request.PageSize, 50);
            var from = (page - 1) * pageSize;

            var searchResponse = await _client.SearchAsync<SchoolDocument>(s => s
                .Indices("be_search_v1")
                .From(from)
                .Size(pageSize)
                .Query(q => q
                    .Bool(b =>
                    {
                        var filters = new List<Action<QueryDescriptor<SchoolDocument>>>
                        {
                            f => f.Term(t => t.Field("doc_type").Value("school")),
                            f => f.Term(t => t.Field("is_active").Value(true))
                        };

                        if (request.CityId.HasValue)
                            filters.Add(f => f.Term(t => t.Field("city_id").Value(request.CityId.Value)));

                        if (request.LocalityId.HasValue)
                            filters.Add(f => f.Term(t => t.Field("locality_id").Value(request.LocalityId.Value)));

                        if (request.CoedId.HasValue)
                            filters.Add(f => f.Term(t => t.Field("coed_id").Value(request.CoedId.Value)));

                        if (request.OwnershipId.HasValue)
                            filters.Add(f => f.Term(t => t.Field("ownership_id").Value(request.OwnershipId.Value)));

                        if (request.NsewcId.HasValue)
                            filters.Add(f => f.Term(t => t.Field("nsewc_id").Value(request.NsewcId.Value)));

                        b.Filter(filters.ToArray());

                        if (!string.IsNullOrWhiteSpace(request.Q))
                        {
                            b.Must(m => m.Bool(inner => inner
                                .Should(
                                    sh => sh.MultiMatch(mm => mm
                                        .Fields(new[] { "title", "keywords", "description" })
                                        .Query(request.Q)
                                        .Type(TextQueryType.BestFields)
                                        .Fuzziness(new Fuzziness("AUTO"))
                                    ),
                                    sh => sh.MatchPhrasePrefix(mp => mp
                                        .Field("title")
                                        .Query(request.Q)
                                    )
                                )
                                .MinimumShouldMatch(1)
                            ));
                        }
                    })
                )
                .Sort(so => so
                    .Score(sc => sc.Order(SortOrder.Desc))
                    .Field("is_featured", fs => fs.Order(SortOrder.Desc))
                    .Field("listing_rank", fs => fs.Order(SortOrder.Desc))
                )
            );

            if (!searchResponse.IsValidResponse)
            {
                return StatusCode(500, new
                {
                    error = "Elasticsearch query failed",
                    details = searchResponse.ElasticsearchServerError?.ToString(),
                    debug = searchResponse.DebugInformation
                });
            }

            var result = new SchoolSearchResponse
            {
                Total = (int)searchResponse.Total,
                Page = page,
                PageSize = pageSize,
                Items = searchResponse.Documents.Select(x => new SchoolSearchItem
                {
                    Id = x.Id,
                    DocType = x.DocType,
                    EntityId = x.EntityId,
                    Title = x.Title,
                    Slug = x.Slug,
                    Url = x.Url,
                    LocalityId = x.LocalityId ?? 0,
                    LocalityName = x.LocalityName,
                    CityId = x.CityId ?? 0,
                    CityName = x.CityName,
                    NsewcId = x.NsewcId ?? 0,
                    CoedId = x.CoedId ?? 0,
                    OwnershipId = x.OwnershipId ?? 0,
                    IsActive = x.IsActive,
                    IsFeatured = x.IsFeatured,
                    ListingRank = x.ListingRank,
                    Keywords = x.Keywords,
                    Description = x.Description
                }).ToList()
            };

            return Ok(result);
        }

        [HttpGet("suggest")]
        public async Task<IActionResult> Suggest([FromQuery] string q, [FromQuery] int size = 10)
        {
            if (string.IsNullOrWhiteSpace(q))
                return Ok(new List<object>());

            q = q.Trim();
            size = size <= 0 ? 10 : Math.Min(size, 20);

            var response = await _client.SearchAsync<SchoolDocument>(s => s
                .Indices("be_search_v1")
                .Size(size)
                .Query(qry => qry
                    .Bool(b => b
                        .Filter(
                            f => f.Term(t => t.Field("doc_type").Value("school")),
                            f => f.Term(t => t.Field("is_active").Value(true))
                        )
                        .Must(m => m.Bool(inner => inner
                            .Should(
                                sh => sh.MatchPhrasePrefix(mp => mp
                                    .Field("title")
                                    .Query(q)
                                    .Boost(8)
                                ),
                                sh => sh.Match(mt => mt
                                    .Field("title")
                                    .Query(q)
                                    .Boost(6)
                                ),
                                sh => sh.Match(mt => mt
                                    .Field("keywords")
                                    .Query(q)
                                    .Boost(3)
                                ),
                                sh => sh.Match(mt => mt
                                    .Field("description")
                                    .Query(q)
                                    .Boost(1)
                                )
                            )
                            .MinimumShouldMatch(1)
                        ))
                    )
                )
                .Sort(so => so
                    .Score(sc => sc.Order(SortOrder.Desc))
                )
            );

            if (!response.IsValidResponse)
            {
                return StatusCode(500, new
                {
                    error = "Elasticsearch suggest failed",
                    details = response.ElasticsearchServerError?.ToString(),
                    debug = response.DebugInformation
                });
            }

            var suggestions = response.Documents
                .Where(x => !string.IsNullOrWhiteSpace(x.Title))
                .GroupBy(x => x.Title)
                .Select(g => g.First())
                .Take(size)
                .Select(x => new
                {
                    title = x.Title,
                    url = x.Url,
                    locality = x.LocalityName,
                    city = x.CityName
                })
                .ToList();

            return Ok(suggestions);
        }

        [HttpPost("index")]
        public async Task<IActionResult> IndexDocument([FromBody] SchoolDocument doc)
        {
            if (doc == null || string.IsNullOrWhiteSpace(doc.Id))
            {
                return BadRequest(new { error = "Document Id is required." });
            }

            var response = await _client.IndexAsync(doc, idx => idx
                .Index("be_search_v1")
                .Id(doc.Id)
            );

            if (!response.IsValidResponse)
            {
                return StatusCode(500, new
                {
                    error = "Failed to index document",
                    details = response.ElasticsearchServerError?.ToString(),
                    debug = response.DebugInformation
                });
            }

            return Ok(new
            {
                indexed = doc.Id,
                result = response.Result.ToString()
            });
        }

        private static IEnumerable<string> BuildAliases(School s)
        {
            var aliases = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            var name = s.InstituteName?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(name))
                return aliases;

            aliases.Add(name);

            if (name.Contains("Delhi Public School", StringComparison.OrdinalIgnoreCase))
            {
                aliases.Add("DPS");
                aliases.Add("Delhi Public School");
                aliases.Add(name.Replace("Delhi Public School", "DPS", StringComparison.OrdinalIgnoreCase));

                if (!string.IsNullOrWhiteSpace(s.City?.CityName))
                {
                    aliases.Add($"DPS {s.City.CityName}");
                    aliases.Add($"Delhi Public School {s.City.CityName}");
                }

                if (!string.IsNullOrWhiteSpace(s.Locality?.LocalityName))
                {
                    aliases.Add($"DPS {s.Locality.LocalityName}");
                    aliases.Add($"Delhi Public School {s.Locality.LocalityName}");
                }
            }

            if (name.Contains("National Public School", StringComparison.OrdinalIgnoreCase))
            {
                aliases.Add("NPS");
                aliases.Add("National Public School");
                aliases.Add(name.Replace("National Public School", "NPS", StringComparison.OrdinalIgnoreCase));
            }

            return aliases;
        }

        [HttpPost("reindex")]
        public async Task<IActionResult> ReindexAll()
        {
            var schools = await _context.Schools
                .Include(s => s.City)
                .Include(s => s.Locality)
                .Include(s => s.Coed)
                .Include(s => s.Ownership)
                .Include(s => s.NsewcNav)
                .Include(s => s.Syllabus)
                .Where(s => s.IsActive && s.InstituteId > 0 && !string.IsNullOrWhiteSpace(s.InstituteName))
                .OrderByDescending(s => s.ListingRank ?? 0)
                .ThenBy(s => s.InstituteName)
                .ToListAsync();

            var docs = schools
                .Select(s =>
                {
                    var aliases = BuildAliases(s);

                    var keywordParts = new List<string?>
                    {
                        s.InstituteName,
                        s.Keyword,
                        s.Syllabus?.SyllabusName,
                        s.MetaDescription,
                        s.City?.CityName,
                        s.Locality?.LocalityName,
                        s.NsewcNav?.NsewcName,
                        s.Ownership?.InstOwnershipType,
                        s.Coed?.CoedName
                    };

                    keywordParts.AddRange(aliases);

                    return new SchoolDocument
                    {
                        Id = $"school-{s.InstituteId}",
                        DocType = "school",
                        EntityId = s.InstituteId,
                        Title = s.InstituteName,
                        Slug = s.InstituteSlug,
                        Url = !string.IsNullOrWhiteSpace(s.InstituteSlug)
                            ? $"/school/{s.InstituteSlug}"
                            : $"/school-{s.InstituteId}",
                        LocalityId = s.LocalityId,
                        LocalityName = s.Locality?.LocalityName,
                        CityId = s.CityId,
                        CityName = s.City?.CityName,
                        NsewcId = s.NsewcId,
                        CoedId = s.CoedId,
                        OwnershipId = s.InstOwnershipId,
                        IsActive = s.IsActive,
                        IsFeatured = s.IsSponsored,
                        ListingRank = s.ListingRank ?? 0,
                        Keywords = string.Join(" ", keywordParts.Where(x => !string.IsNullOrWhiteSpace(x))),
                        Description = !string.IsNullOrWhiteSpace(s.MetaDescription)
                            ? s.MetaDescription
                            : string.Join(" ", new[]
                            {
                                s.InstituteName,
                                s.Syllabus?.SyllabusName,
                                s.City?.CityName,
                                s.Locality?.LocalityName
                            }.Where(x => !string.IsNullOrWhiteSpace(x)))
                    };
                })
                .ToList();

            var indexedIds = new List<string>();
            var failed = new List<object>();

            foreach (var doc in docs)
            {
                var response = await _client.IndexAsync(doc, idx => idx
                    .Index("be_search_v1")
                    .Id(doc.Id));

                if (response.IsValidResponse)
                {
                    indexedIds.Add(doc.Id!);
                }
                else
                {
                    failed.Add(new
                    {
                        id = doc.Id,
                        error = response.ElasticsearchServerError?.ToString(),
                        debug = response.DebugInformation
                    });
                }
            }

            return Ok(new
            {
                total = docs.Count,
                indexed = indexedIds.Count,
                failed = failed.Count,
                failures = failed
            });
        }

        [HttpPost("clear")]
        public async Task<IActionResult> ClearIndex()
        {
            var response = await _client.DeleteByQueryAsync<SchoolDocument>(d => d
                .Indices("be_search_v1")
                .Query(q => q.MatchAll(new MatchAllQuery()))
            );

            if (!response.IsValidResponse)
            {
                return StatusCode(500, new
                {
                    error = "Failed to clear index",
                    details = response.ElasticsearchServerError?.ToString(),
                    debug = response.DebugInformation
                });
            }

            return Ok(new
            {
                deleted = response.Deleted
            });
        }
    }
}