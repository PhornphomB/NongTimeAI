# Changelog

All notable changes to NongTimeAI project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [1.0.0] - 2024-01-XX

### Added
- ? Initial release of NongTimeAI
- ? AI-powered Thai language timesheet extraction using Llama 3.2
- ? Support for 3 issue types: Task, Issue, Meeting
- ? Automatic data completeness validation
- ? Friendly reminder message generation
- ? RESTful API with 2 main endpoints:
  - `POST /api/timesheet/process` - Process timesheet messages
  - `POST /api/timesheet/reminder` - Generate reminder messages
- ? Health check endpoints:
  - `GET /api/health` - Service health status
  - `GET /api/health/ollama` - Ollama connectivity check
- ? Docker and Docker Compose support
- ? CORS configuration for LINE Bot integration
- ? Comprehensive documentation:
  - README.md - Main documentation
  - QUICKSTART.md - Quick start guide
  - DOCKER.md - Docker usage guide
  - LINE_INTEGRATION.md - LINE Bot integration guide
- ? JSON response cleansing for robust parsing
- ? Fallback logic for incomplete data
- ? Unit tests with xUnit and Moq
- ? Environment variable template (.env.template)

### Technical Details
- Target Framework: .NET 9.0
- AI Model: Llama 3.2 via Ollama
- Temperature: 0.1 (extraction), 0.7 (reminder generation)
- JSON parsing with snake_case support

### Configuration
- Ollama URL: Configurable via appsettings.json
- Model selection: Configurable
- CORS: Allow all origins (configurable)

### Documentation
- Complete API documentation
- Docker deployment guide
- LINE Bot integration examples
- Hangfire job scheduling examples
- Rich Menu configuration examples

## [Unreleased]

### Planned Features
- Database integration for timesheet storage
- User authentication and authorization
- Multi-project support
- Weekly/Monthly timesheet reports
- Export to Excel/PDF
- Slack Bot integration
- Microsoft Teams Bot integration
- Dashboard for timesheet analytics
- Mobile app support
- Multi-language support (English, Thai)

---

### Legend
- ? Completed
- ?? In Progress
- ?? Planned
