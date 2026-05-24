from __future__ import annotations

from dataclasses import dataclass
from typing import Final

MIN_STORY_LENGTH: Final[int] = 100
MAX_STORY_LENGTH: Final[int] = 4000

STOP_WORDS: Final[frozenset[str]] = frozenset(
    {
        "призрак",
        "нло",
        "мистика",
        "проклятие",
        "инопланетян",
        "полтергейст",
        "магия",
    }
)


@dataclass(frozen=True)
class ParsedStory:
    source_title: str
    text: str
    source_url: str


def normalize_text(text: str) -> str:
    return " ".join(text.split())


def contains_stop_word(text: str) -> bool:
    lowered = text.lower()
    return any(stop_word in lowered for stop_word in STOP_WORDS)


def is_valid_story_length(text: str) -> bool:
    length = len(text)
    return MIN_STORY_LENGTH <= length <= MAX_STORY_LENGTH


def validate_story(title: str, text: str, url: str) -> ParsedStory | None:
    normalized_title = normalize_text(title)
    normalized_text = normalize_text(text)
    normalized_url = url.strip()

    if not normalized_title:
        return None

    if not normalized_url:
        return None

    if not is_valid_story_length(normalized_text):
        return None

    combined_text = f"{normalized_title} {normalized_text}"
    if contains_stop_word(combined_text):
        return None

    return ParsedStory(
        source_title=normalized_title,
        text=normalized_text,
        source_url=normalized_url,
    )
