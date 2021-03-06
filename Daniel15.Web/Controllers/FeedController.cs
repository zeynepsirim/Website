﻿using System.Collections.Generic;
using System.Linq;
using System.Web.Mvc;
using AttributeRouting.Web.Mvc;
using Daniel15.BusinessLayer.Services;
using Daniel15.Data;
using Daniel15.Data.Entities.Blog;
using Daniel15.Data.Repositories;
using Daniel15.Infrastructure;
using Daniel15.Web.ViewModels.Blog;
using Daniel15.Web.ViewModels.Feed;
using Daniel15.Web.Extensions;

namespace Daniel15.Web.Controllers
{
	/// <summary>
	/// Handles rendering various feeds
	/// </summary>
	public partial class FeedController : Controller
	{
		/// <summary>
		/// Number of blog posts to show in the RSS feed
		/// </summary>
		private const int ITEMS_IN_FEED = 10;

		private readonly IBlogRepository _blogRepository;
		private readonly IProjectRepository _projectRepository;
		private readonly ISiteConfiguration _siteConfig;
		private readonly IUrlShortener _urlShortener;

		/// <summary>
		/// Initializes a new instance of the <see cref="FeedController" /> class.
		/// </summary>
		/// <param name="blogRepository">The blog repository.</param>
		/// <param name="projectRepository">Project repository</param>
		/// <param name="siteConfig">Site configuration</param>
		/// <param name="urlShortener">URL shortener</param>
		public FeedController(IBlogRepository blogRepository, IProjectRepository projectRepository, ISiteConfiguration siteConfig, IUrlShortener urlShortener)
		{
			_blogRepository = blogRepository;
			_projectRepository = projectRepository;
			_siteConfig = siteConfig;
			_urlShortener = urlShortener;
		}

		/// <summary>
		/// Gets a sitemap for the website
		/// </summary>
		/// <returns>Sitemap XML</returns>
		[GET("sitemap.xml")]
		public virtual ActionResult Sitemap()
		{
			Response.ContentType = "text/xml";
			return View(new SitemapViewModel
			{
				Posts = _blogRepository.LatestPostsSummary(10000), // Should be big enough, lols
				Categories = _blogRepository.Categories(),
				Tags = _blogRepository.Tags(),
				Projects = _projectRepository.All()
			});
		}

		/// <summary>
		/// RSS feed of all the latest posts
		/// </summary>
		/// <returns>RSS feed</returns>
		[GET("blog/feed")]
		public virtual ActionResult BlogLatest()
		{
			if (Request.ShouldRedirectToFeedburner())
				return Redirect(_siteConfig.FeedBurnerUrl.ToString());

			var posts = _blogRepository.LatestPosts(ITEMS_IN_FEED);
			return RenderFeed(posts, new FeedViewModel
			{
				FeedGuidBase = "Latest",
				Title = _siteConfig.BlogName,
				Description = _siteConfig.BlogDescription,
				FeedUrl = _siteConfig.FeedBurnerUrl.ToString(),
				SiteUrl = Url.ActionAbsolute(MVC.Blog.Index())
			});
		}

		/// <summary>
		/// RSS feed for a specific category
		/// </summary>
		/// <param name="slug">Category slug</param>
		/// <returns>RSS feed</returns>
		[GET("category/{parentSlug}/{slug}.rss", ActionPrecedence = 1, RouteName = "BlogSubCategoryFeed")]
		[GET("category/{slug}.rss", ActionPrecedence = 2, RouteName = "BlogCategoryFeed")]
		public virtual ActionResult BlogCategory(string slug, string parentSlug = null)
		{
			CategoryModel category;
			try
			{
				category = _blogRepository.GetCategory(slug);
			}
			catch (EntityNotFoundException)
			{
				// Throw a 404 if the category doesn't exist
				return HttpNotFound(string.Format("Category '{0}' not found.", slug));
			}

			// If the category has a parent category, ensure it's in the URL
			if (!string.IsNullOrEmpty(category.ParentSlug) && string.IsNullOrEmpty(parentSlug))
			{
				return RedirectToActionPermanent(MVC.Feed.BlogCategory(slug, category.ParentSlug));
			}

			var posts = _blogRepository.LatestPosts(category, ITEMS_IN_FEED);
			return RenderFeed(posts, new FeedViewModel
			{
				FeedGuidBase = "Category-" + category.Slug + "-",
				Title = category.Title + " - " + _siteConfig.BlogName,
				Description = category.Title + " posts to " + _siteConfig.BlogName,
				FeedUrl = Url.Absolute(Url.BlogCategoryFeed(category)),
				SiteUrl = Url.Absolute(Url.BlogCategory(category))
			});
		}

		/// <summary>
		/// Renders the specified list of posts to an RSS feed
		/// </summary>
		/// <param name="posts">Posts to render</param>
		/// <param name="model">View model to render with</param>
		/// <returns>RSS feed</returns>
		private ActionResult RenderFeed(IList<PostModel> posts, FeedViewModel model)
		{
			Response.ContentType = "application/rss+xml";
			// Set last-modified date based on the date of the newest post
			if (posts.Count > 0)
				Response.Cache.SetLastModified(posts[0].Date);

			var categories = _blogRepository.CategoriesForPosts(posts);
			model.Posts = posts.Select(post => new PostViewModel
			{
				Post = post,
				ShortUrl = ShortUrl(post),
				PostCategories = categories[post]
			}).ToList();

			return View(Views.Blog, model);
		}

		/// <summary>
		/// Gets the short URL for this blog post
		/// </summary>
		/// <param name="post">Blog post</param>
		/// <returns>The short URL</returns>
		private string ShortUrl(PostModel post)
		{
			return Url.Action(MVC.Blog.ShortUrl(_urlShortener.Shorten(post)), "http");
		}
	}
}
