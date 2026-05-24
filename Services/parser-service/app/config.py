from pydantic import Field
from pydantic_settings import BaseSettings, SettingsConfigDict


class Settings(BaseSettings):
    model_config = SettingsConfigDict(
        env_file=".env",
        env_file_encoding="utf-8",
        extra="ignore",
    )

    mongo_uri: str = Field(alias="MONGO_URI")
    mongo_db_name: str = Field(default="parser_db", alias="MONGO_DB")
    mongo_collection: str = Field(default="stories", alias="MONGO_COLLECTION")

    kafka_bootstrap_servers: str = Field(alias="KAFKA_BOOTSTRAP_SERVERS")
    kafka_topic_output: str = Field(default="stories.raw", alias="KAFKA_TOPIC_OUTPUT")

    grpc_host: str = Field(default="0.0.0.0", alias="GRPC_HOST")
    grpc_port: int = Field(default=50053, alias="GRPC_PORT")

    http_timeout: float = Field(default=30.0, alias="HTTP_TIMEOUT")
    scraper_user_agent: str = Field(
        default="DanetkaParser/1.0",
        alias="SCRAPER_USER_AGENT",
    )
    default_parse_limit: int = Field(default=10, alias="DEFAULT_PARSE_LIMIT")


def get_settings() -> Settings:
    return Settings()
