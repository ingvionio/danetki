from __future__ import annotations

import logging
from typing import Final
from urllib.parse import urljoin

import httpx
from bs4 import BeautifulSoup, Tag

from app.config import Settings
from app.scraper.validator import ParsedStory, validate_story

logger = logging.getLogger(__name__)

FACTROOM_BASE_URL: Final[str] = "https://factroom.ru"
FACTROOM_LIST_PATH: Final[str] = "/"


class FactroomScraper:
    def __init__(self, settings: Settings) -> None:
        self._settings = settings
        self._headers = {"User-Agent": settings.scraper_user_agent}

    async def fetch_stories(self, limit: int, source_url: str = "") -> list[ParsedStory]:
        async with httpx.AsyncClient(
            timeout=self._settings.http_timeout,
            headers=self._headers,
            follow_redirects=True,
        ) as client:
            list_url = self._resolve_list_url(source_url)
            response = await client.get(list_url)
            response.raise_for_status()

            article_links = self._extract_article_links(response.text)
            stories: list[ParsedStory] = []

            for link in article_links:
                if len(stories) >= limit:
                    break

                story = await self._fetch_article(client, link)
                if story is None:
                    continue

                stories.append(story)

            return stories

    def _resolve_list_url(self, source_url: str) -> str:
        normalized_url = source_url.strip()
        if normalized_url:
            return normalized_url

        return urljoin(FACTROOM_BASE_URL, FACTROOM_LIST_PATH)

    def _extract_article_links(self, html: str) -> list[str]:
        soup = BeautifulSoup(html, "html.parser")
        links: list[str] = []

        for anchor in soup.select("article a[href], .post-title a[href], h2 a[href]"):
            if not isinstance(anchor, Tag):
                continue

            href = anchor.get("href")
            if not href or not isinstance(href, str):
                continue

            full_url = urljoin(FACTROOM_BASE_URL, href)
            if full_url in links:
                continue

            if "/category/" in full_url:
                continue

            links.append(full_url)

        return links

    async def _fetch_article(
        self,
        client: httpx.AsyncClient,
        url: str,
    ) -> ParsedStory | None:
        try:
            response = await client.get(url)
            response.raise_for_status()
        except httpx.HTTPError:
            logger.warning("Failed to fetch article: %s", url)
            return None

        return self._parse_article_html(response.text, url)

    def _parse_article_html(self, html: str, url: str) -> ParsedStory | None:
        soup = BeautifulSoup(html, "html.parser")

        title_tag = soup.select_one("h1.entry-title, h1.post-title, h1")
        if title_tag is None:
            return None

        content_tag = soup.select_one(
            ".entry-content, .post-content, article .content, .text-content"
        )
        if content_tag is None:
            return None

        for unwanted in content_tag.select("script, style, noscript, .ads, .share"):
            unwanted.decompose()

        paragraphs = [
            paragraph.get_text(strip=True)
            for paragraph in content_tag.find_all("p")
            if paragraph.get_text(strip=True)
        ]
        if not paragraphs:
            text = content_tag.get_text(separator=" ", strip=True)
        else:
            text = " ".join(paragraphs)

        return validate_story(title_tag.get_text(strip=True), text, url)


class ScraperRegistry:
    @classmethod
    def get_scraper(cls, source_url: str, settings: Settings) -> FactroomScraper:
        if source_url and "factroom" not in source_url:
            raise ValueError(f"Unsupported source: {source_url}")

        return FactroomScraper(settings)

    @classmethod
    async def fetch_stories(
        cls,
        source_url: str,
        limit: int,
        settings: Settings,
    ) -> list[ParsedStory]:
        scraper = cls.get_scraper(source_url, settings)
        return await scraper.fetch_stories(limit=limit, source_url=source_url)
