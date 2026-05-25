using Microsoft.AspNetCore.Mvc;
using SchoolProject.Services.Search;

namespace SchoolProject.Controllers
{
    public class SearchAdminController : Controller
    {
        private readonly IElasticSearchService _elasticSearchService;

        public SearchAdminController(IElasticSearchService elasticSearchService)
        {
            _elasticSearchService = elasticSearchService;
        }

        [HttpPost]
        [Route("admin/search/rebuild-index")]
        public async Task<IActionResult> RebuildIndex(CancellationToken cancellationToken)
        {
            await _elasticSearchService.RebuildIndexAsync(cancellationToken);
            return Ok(new { success = true, message = "Search index rebuild triggered." });
        }
    }
}
