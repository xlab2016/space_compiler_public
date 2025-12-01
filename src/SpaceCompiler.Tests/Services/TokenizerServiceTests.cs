using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using SpaceCompiler.Services;
using Xunit;

namespace SpaceCompiler.Tests.Services
{
    public class TokenizerServiceTests
    {
        private readonly Mock<ILogger<TokenizerService>> _loggerMock;
        private readonly TokenizerService _service;

        public TokenizerServiceTests()
        {
            _loggerMock = new Mock<ILogger<TokenizerService>>();
            _service = new TokenizerService(_loggerMock.Object);
        }

        [Fact]
        public async Task TokenizeAsync_WithTextContent_ShouldCreateFragments()
        {
            // Arrange
            var content = "This is a normal paragraph with enough content to meet the minimum length requirement for fragment creation.";

            // Act
            var result = await _service.TokenizeAsync(content, "text");

            // Assert
            result.Should().NotBeNull();
            result.Should().HaveCount(1);
            result[0].Content.Should().Contain("normal paragraph");
            result[0].Type.Should().Be("paragraph");
            result[0].Order.Should().Be(0);
        }

        [Fact]
        public async Task TokenizeAsync_WithMultipleParagraphs_ShouldCreateMultipleFragments()
        {
            // Arrange
            var content = @"First paragraph with sufficient content to meet requirements.

Second paragraph that also has enough text to be considered valid.

Third paragraph continuing the pattern of adequate length.";

            // Act
            var result = await _service.TokenizeAsync(content, "text");

            // Assert
            result.Should().HaveCount(3);
            result[0].Content.Should().Contain("First paragraph");
            result[1].Content.Should().Contain("Second paragraph");
            result[2].Content.Should().Contain("Third paragraph");
            result[0].Order.Should().Be(0);
            result[1].Order.Should().Be(1);
            result[2].Order.Should().Be(2);
        }

        [Fact]
        public async Task TokenizeAsync_WithShortParagraphs_ShouldMergeThem()
        {
            // Arrange
            var content = @"Short one.

Short two.

Short three.";

            // Act
            var result = await _service.TokenizeAsync(content, "text");

            // Assert
            result.Should().HaveCount(1);
            result[0].Content.Should().Contain("Short one");
            result[0].Content.Should().Contain("Short two");
            result[0].Content.Should().Contain("Short three");
        }

        [Fact]
        public async Task TokenizeAsync_WithJsonContent_ShouldParseJsonStructure()
        {
            // Arrange
            var content = @"{
                ""name"": ""Test"",
                ""value"": 123,
                ""items"": [1, 2, 3]
            }";

            // Act
            var result = await _service.TokenizeAsync(content, "json");

            // Assert
            result.Should().NotBeEmpty();
            result.Should().Contain(f => f.Type == "json_object");
        }

        [Fact]
        public void GetSupportedContentTypes_ShouldReturnKnownTypes()
        {
            // Act
            var types = _service.GetSupportedContentTypes();

            // Assert
            types.Should().Contain("text");
            types.Should().Contain("json");
        }

        [Fact]
        public async Task TokenizeAsync_WithNumberedHeadings_ShouldDetectStructure()
        {
            // Arrange
            var content = @"1. ОБЩАЯ ХАРАКТЕРИСТИКА ВИДА РАБОТЫ

Это первый раздел документа с достаточным количеством текста для фрагмента.

1.1. Цели и задачи работы

Подробное описание целей и задач с достаточным количеством текста.

2. Методология исследования

Описание методологии с достаточным количеством текста для создания отдельного фрагмента.";

            // Act
            var result = await _service.TokenizeAsync(content, "text");

            // Assert
            result.Should().NotBeEmpty();

            // Should have heading fragments
            var heading1 = result.FirstOrDefault(f => f.Type == "heading_1");
            heading1.Should().NotBeNull();
            heading1!.Content.Should().Contain("ОБЩАЯ ХАРАКТЕРИСТИКА");
            heading1.Metadata.Should().ContainKey("numbering");
            heading1.Metadata["numbering"].Should().Be("1");

            var heading2 = result.FirstOrDefault(f => f.Type == "heading_2");
            heading2.Should().NotBeNull();
            heading2!.Content.Should().Contain("Цели и задачи");
            heading2.Metadata.Should().ContainKey("numbering");
            heading2.Metadata["numbering"].Should().Be("1.1");

            // Should have regular paragraph fragments
            result.Should().Contain(f => f.Type == "paragraph");
        }

        [Fact]
        public async Task TokenizeAsync_WithTableOfContents_ShouldDetectTocStructure()
        {
            // Arrange
            var content = @"Содержание

1. Введение .......................... 5

2. Основная часть .................... 10

  2.1. Методология ................... 15

  2.2. Результаты .................... 20

3. Заключение ........................ 25

Это начало основного текста документа с достаточным количеством содержимого для создания фрагмента.";

            // Act
            var result = await _service.TokenizeAsync(content, "text");

            // Assert
            result.Should().NotBeEmpty();

            // Should have TOC header
            var tocHeader = result.FirstOrDefault(f => f.Type == "toc_header");
            tocHeader.Should().NotBeNull();
            tocHeader!.Content.Should().Be("Содержание");

            // Should have TOC entries
            var tocEntries = result.Where(f => f.Type == "toc_entry").ToList();
            tocEntries.Should().NotBeEmpty();
            tocEntries.Should().Contain(e => e.Content.Contains("Введение"));
            tocEntries.Should().Contain(e => e.Content.Contains("Основная часть"));

            // Should have regular paragraph after TOC
            var regularParagraphs = result.Where(f => f.Type == "paragraph").ToList();
            regularParagraphs.Should().NotBeEmpty();
        }

        [Fact]
        public async Task TokenizeAsync_WithChapterMarkers_ShouldDetectChapters()
        {
            // Arrange
            var content = @"Глава 1. Введение

Текст введения с достаточным количеством содержимого для создания фрагмента параграфа.

Часть I. Теоретическая часть

Описание теоретической части с достаточным количеством текста для фрагмента.

Раздел 2. Практическая часть

Описание практической части работы с достаточным количеством содержимого.";

            // Act
            var result = await _service.TokenizeAsync(content, "text");

            // Assert
            result.Should().NotBeEmpty();

            // Should detect chapter markers
            var chapters = result.Where(f => f.Type == "chapter").ToList();
            chapters.Count.Should().BeGreaterThanOrEqualTo(2);
            chapters.Should().Contain(c => c.Content.Contains("Глава 1"));
            chapters.Should().Contain(c => c.Content.Contains("Часть I"));

            // Should have metadata with numbering
            var firstChapter = chapters.First(c => c.Content.Contains("Глава 1"));
            firstChapter.Metadata.Should().ContainKey("numbering");
            firstChapter.Metadata["numbering"].Should().Be("1");
        }

        [Fact]
        public async Task TokenizeAsync_WithRomanNumerals_ShouldDetectRomanHeadings()
        {
            // Arrange
            var content = @"I. Введение в тему

Вводный текст с достаточным количеством содержимого для создания параграфа.

II. Основная часть исследования

Текст основной части с достаточным количеством содержимого для фрагмента.

III. Заключительные выводы

Заключительный текст с достаточным количеством содержимого для параграфа.";

            // Act
            var result = await _service.TokenizeAsync(content, "text");

            // Assert
            result.Should().NotBeEmpty();

            // Should detect Roman numeral headings
            var romanHeadings = result.Where(f => f.Type == "heading_roman").ToList();
            romanHeadings.Should().HaveCount(3);
            romanHeadings.Should().Contain(h => h.Content.Contains("Введение"));
            romanHeadings.Should().Contain(h => h.Content.Contains("Основная часть"));
            romanHeadings.Should().Contain(h => h.Content.Contains("Заключительные выводы"));

            // Check numbering metadata
            var firstHeading = romanHeadings.First(h => h.Content.Contains("Введение"));
            firstHeading.Metadata.Should().ContainKey("numbering");
            firstHeading.Metadata["numbering"].Should().Be("I");
        }

        [Fact]
        public async Task TokenizeAsync_WithUppercaseHeadings_ShouldDetectUppercaseHeadings()
        {
            // Arrange
            var content = @"ВВЕДЕНИЕ

Вводный текст документа с достаточным количеством содержимого для создания параграфа.

ОБЩАЯ ХАРАКТЕРИСТИКА РАБОТЫ

Описание общей характеристики с достаточным количеством текста для фрагмента.

ЗАКЛЮЧЕНИЕ

Заключительная часть с достаточным количеством содержимого для параграфа.";

            // Act
            var result = await _service.TokenizeAsync(content, "text");

            // Assert
            result.Should().NotBeEmpty();

            // Should detect uppercase headings
            var uppercaseHeadings = result.Where(f => f.Type == "heading_uppercase").ToList();
            uppercaseHeadings.Should().HaveCount(3);
            uppercaseHeadings.Should().Contain(h => h.Content == "ВВЕДЕНИЕ");
            uppercaseHeadings.Should().Contain(h => h.Content == "ОБЩАЯ ХАРАКТЕРИСТИКА РАБОТЫ");
            uppercaseHeadings.Should().Contain(h => h.Content == "ЗАКЛЮЧЕНИЕ");
        }

        [Fact]
        public async Task TokenizeAsync_WithComplexDocumentStructure_ShouldPreserveHierarchy()
        {
            // Arrange
            var content = @"АВТОРЕФЕРАТ

1. ОБЩАЯ ХАРАКТЕРИСТИКА ВИДА РАБОТЫ

Общая характеристика представляет собой описание основных аспектов работы.

1.1. Актуальность темы исследования

Актуальность обусловлена необходимостью изучения данной проблематики в современных условиях.

1.2. Цели и задачи работы

Целью работы является комплексное исследование выбранной тематики.

2. МЕТОДОЛОГИЯ И МЕТОДЫ ИССЛЕДОВАНИЯ

В работе использовались следующие методы исследования и анализа данных.";

            // Act
            var result = await _service.TokenizeAsync(content, "text");

            // Assert
            result.Should().NotBeEmpty();

            // Verify order is preserved
            var types = result.Select(f => f.Type).ToList();

            // Should start with uppercase heading
            types[0].Should().Be("heading_uppercase");
            result[0].Content.Should().Be("АВТОРЕФЕРАТ");

            // Should have level 1 and level 2 headings
            result.Should().Contain(f => f.Type == "heading_1" && f.Content.Contains("ОБЩАЯ ХАРАКТЕРИСТИКА"));
            result.Should().Contain(f => f.Type == "heading_2" && f.Content.Contains("Актуальность"));
            result.Should().Contain(f => f.Type == "heading_2" && f.Content.Contains("Цели и задачи"));

            // Verify hierarchy in order
            for (int i = 0; i < result.Count - 1; i++)
            {
                result[i].Order.Should().BeLessThan(result[i + 1].Order);
            }
        }

        [Fact]
        public async Task TokenizeAsync_WithIndentedContent_ShouldDetectIndentation()
        {
            // Arrange
            var content = @"Основной текст без отступа с достаточным количеством содержимого.

  Текст с отступом в два пробела, достаточно длинный для фрагмента.

    Текст с отступом в четыре пробела, также достаточно длинный.

Возврат к тексту без отступа с достаточным количеством содержимого.";

            // Act
            var result = await _service.TokenizeAsync(content, "text");

            // Assert
            result.Should().NotBeEmpty();

            // Should detect indented fragments
            var indentedFragments = result.Where(f => f.Type == "indented").ToList();
            indentedFragments.Count.Should().BeGreaterThanOrEqualTo(2);

            // Check indentation levels in metadata
            var fragments = result.Where(f => f.Metadata?.ContainsKey("indent_level") == true).ToList();
            fragments.Should().NotBeEmpty();
        }

        [Fact]
        public async Task TokenizeAsync_WithMixedEnglishAndRussianHeadings_ShouldDetectBoth()
        {
            // Arrange
            var content = @"Chapter 1. Introduction

This is an introduction paragraph with sufficient content for fragment creation.

Глава 2. Методология

Это параграф методологии с достаточным количеством содержимого для фрагмента.

Section 3. Results and Discussion

Results section with enough content to create a proper fragment for testing.";

            // Act
            var result = await _service.TokenizeAsync(content, "text");

            // Assert
            result.Should().NotBeEmpty();

            // Should detect both English and Russian chapters
            var chapters = result.Where(f => f.Type == "chapter").ToList();
            chapters.Count.Should().BeGreaterThanOrEqualTo(2);
            chapters.Should().Contain(c => c.Content.Contains("Introduction"));
            chapters.Should().Contain(c => c.Content.Contains("Методология"));
        }
    }
}
