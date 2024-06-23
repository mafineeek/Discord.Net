// using Discord.Utils;
// using System.Collections.Immutable;
// using System.Text.Json;
//
// namespace Discord;
//
// /// <summary>
// ///     Represents a builder class for creating a <see cref="EmbedType.Rich" /> <see cref="Embed" />.
// /// </summary>
// public class EmbedBuilder
// {
//     /// <summary>
//     ///     Returns the maximum number of fields allowed by Discord.
//     /// </summary>
//     public const int MaxFieldCount = 25;
//
//     /// <summary>
//     ///     Returns the maximum length of title allowed by Discord.
//     /// </summary>
//     public const int MaxTitleLength = 256;
//
//     /// <summary>
//     ///     Returns the maximum length of description allowed by Discord.
//     /// </summary>
//     public const int MaxDescriptionLength = 4096;
//
//     /// <summary>
//     ///     Returns the maximum length of total characters allowed by Discord.
//     /// </summary>
//     public const int MaxEmbedLength = 6000;
//
//     private string? _description;
//     private List<EmbedFieldBuilder> _fields;
//     private EmbedImage? _image;
//     private EmbedThumbnail? _thumbnail;
//     private string? _title;
//
//     /// <summary> Initializes a new <see cref="EmbedBuilder" /> class. </summary>
//     public EmbedBuilder()
//     {
//         _fields = new List<EmbedFieldBuilder>();
//     }
//
//     /// <summary> Gets or sets the title of an <see cref="Embed" />. </summary>
//     /// <exception cref="ArgumentException" accessor="set">
//     ///     Title length exceeds <see cref="MaxTitleLength" />.
//     /// </exception>
//     /// <returns> The title of the embed.</returns>
//     public string? Title
//     {
//         get => _title;
//         set
//         {
//             if (value?.Length > MaxTitleLength)
//                 throw new ArgumentException($"Title length must be less than or equal to {MaxTitleLength}.",
//                     nameof(Title));
//             _title = value;
//         }
//     }
//
//     /// <summary> Gets or sets the description of an <see cref="Embed" />. </summary>
//     /// <exception cref="ArgumentException" accessor="set">Description length exceeds <see cref="MaxDescriptionLength" />.</exception>
//     /// <returns> The description of the embed.</returns>
//     public string? Description
//     {
//         get => _description;
//         set
//         {
//             if (value?.Length > MaxDescriptionLength)
//                 throw new ArgumentException($"Description length must be less than or equal to {MaxDescriptionLength}.",
//                     nameof(Description));
//             _description = value;
//         }
//     }
//
//     /// <summary> Gets or sets the URL of an <see cref="Embed" />. </summary>
//     /// <exception cref="ArgumentException" accessor="set">Url is not a well-formed <see cref="Uri" />.</exception>
//     /// <returns> The URL of the embed.</returns>
//     public string? Url { get; set; }
//
//     /// <summary> Gets or sets the thumbnail URL of an <see cref="Embed" />. </summary>
//     /// <exception cref="ArgumentException" accessor="set">Url is not a well-formed <see cref="Uri" />.</exception>
//     /// <returns> The thumbnail URL of the embed.</returns>
//     public string? ThumbnailUrl
//     {
//         get => _thumbnail?.Url;
//         set => _thumbnail = new EmbedThumbnail(value, null, null, null);
//     }
//
//     /// <summary> Gets or sets the image URL of an <see cref="Embed" />. </summary>
//     /// <exception cref="ArgumentException" accessor="set">Url is not a well-formed <see cref="Uri" />.</exception>
//     /// <returns> The image URL of the embed.</returns>
//     public string? ImageUrl
//     {
//         get => _image?.Url;
//         set => _image = new EmbedImage(value, null, null, null);
//     }
//
//     /// <summary> Gets or sets the list of <see cref="EmbedFieldBuilder" /> of an <see cref="Embed" />. </summary>
//     /// <exception cref="ArgumentNullException" accessor="set">
//     ///     An embed builder's fields collection is set to
//     ///     <see langword="null" />.
//     /// </exception>
//     /// <exception cref="ArgumentException" accessor="set">
//     ///     Fields count exceeds <see cref="MaxFieldCount" />.
//     /// </exception>
//     /// <returns> The list of existing <see cref="EmbedFieldBuilder" />.</returns>
//     public List<EmbedFieldBuilder> Fields
//     {
//         get => _fields;
//         set
//         {
//             if (value == null)
//                 throw new ArgumentNullException(nameof(Fields),
//                     "Cannot set an embed builder's fields collection to null.");
//             if (value.Count > MaxFieldCount)
//                 throw new ArgumentException($"Field count must be less than or equal to {MaxFieldCount}.",
//                     nameof(Fields));
//             _fields = value;
//         }
//     }
//
//     /// <summary>
//     ///     Gets or sets the timestamp of an <see cref="Embed" />.
//     /// </summary>
//     /// <returns>
//     ///     The timestamp of the embed, or <see langword="null" /> if none is set.
//     /// </returns>
//     public DateTimeOffset? Timestamp { get; set; }
//
//     /// <summary>
//     ///     Gets or sets the sidebar color of an <see cref="Embed" />.
//     /// </summary>
//     /// <returns>
//     ///     The color of the embed, or <see langword="null" /> if none is set.
//     /// </returns>
//     public Color? Color { get; set; }
//
//     /// <summary>
//     ///     Gets or sets the <see cref="EmbedAuthorBuilder" /> of an <see cref="Embed" />.
//     /// </summary>
//     /// <returns>
//     ///     The author field builder of the embed, or <see langword="null" /> if none is set.
//     /// </returns>
//     public EmbedAuthorBuilder? Author { get; set; }
//
//     /// <summary>
//     ///     Gets or sets the <see cref="EmbedFooterBuilder" /> of an <see cref="Embed" />.
//     /// </summary>
//     /// <returns>
//     ///     The footer field builder of the embed, or <see langword="null" /> if none is set.
//     /// </returns>
//     public EmbedFooterBuilder? Footer { get; set; }
//
//     /// <summary>
//     ///     Gets the total length of all embed properties.
//     /// </summary>
//     /// <returns>
//     ///     The combined length of <see cref="Title" />, <see cref="EmbedAuthor.Name" />, <see cref="Description" />,
//     ///     <see cref="EmbedFooter.Text" />, <see cref="EmbedField.Name" />, and <see cref="EmbedField.Value" />.
//     /// </returns>
//     public int Length
//     {
//         get
//         {
//             var titleLength = Title?.Length ?? 0;
//             var authorLength = Author?.Name?.Length ?? 0;
//             var descriptionLength = Description?.Length ?? 0;
//             var footerLength = Footer?.Text?.Length ?? 0;
//             var fieldSum = Fields.Sum(f => f.Name.Length + (f.Value?.ToString()?.Length ?? 0));
//
//             return titleLength + authorLength + descriptionLength + footerLength + fieldSum;
//         }
//     }
//
//     /// <summary>
//     ///     Tries to parse a string into an <see cref="EmbedBuilder" />.
//     /// </summary>
//     /// <param name="json">The json string to parse.</param>
//     /// <param name="builder">
//     ///     The <see cref="EmbedBuilder" /> with populated values. An empty instance if method returns
//     ///     <see langword="false" />.
//     /// </param>
//     /// <returns><see langword="true" /> if <paramref name="json" /> was successfully parsed. <see langword="false" /> if not.</returns>
//     public static bool TryParse(string json, out EmbedBuilder builder)
//     {
//         builder = new EmbedBuilder();
//         try
//         {
//             var model = JsonSerializer.Deserialize<Embed?>(json);
//
//             if (model is not null)
//             {
//                 builder = model.Value.ToEmbedBuilder();
//                 return true;
//             }
//
//             return false;
//         }
//         catch
//         {
//             return false;
//         }
//     }
//
//     /// <summary>
//     ///     Parses a string into an <see cref="EmbedBuilder" />.
//     /// </summary>
//     /// <param name="json">The json string to parse.</param>
//     /// <returns>An <see cref="EmbedBuilder" /> with populated values from the passed <paramref name="json" />.</returns>
//     /// <exception cref="InvalidOperationException">Thrown if the string passed is not valid json.</exception>
//     public static EmbedBuilder Parse(string json)
//     {
//         var model = JsonSerializer.Deserialize<Embed?>(json);
//
//         if (model is not null)
//             return model.Value.ToEmbedBuilder();
//
//         return new EmbedBuilder();
//     }
//
//     /// <summary>
//     ///     Sets the title of an <see cref="Embed" />.
//     /// </summary>
//     /// <param name="title">The title to be set.</param>
//     /// <returns>
//     ///     The current builder.
//     /// </returns>
//     public EmbedBuilder WithTitle(string? title)
//     {
//         Title = title;
//         return this;
//     }
//
//     /// <summary>
//     ///     Sets the description of an <see cref="Embed" />.
//     /// </summary>
//     /// <param name="description"> The description to be set. </param>
//     /// <returns>
//     ///     The current builder.
//     /// </returns>
//     public EmbedBuilder WithDescription(string? description)
//     {
//         Description = description;
//         return this;
//     }
//
//     /// <summary>
//     ///     Sets the URL of an <see cref="Embed" />.
//     /// </summary>
//     /// <param name="url"> The URL to be set. </param>
//     /// <returns>
//     ///     The current builder.
//     /// </returns>
//     public EmbedBuilder WithUrl(string? url)
//     {
//         Url = url;
//         return this;
//     }
//
//     /// <summary>
//     ///     Sets the thumbnail URL of an <see cref="Embed" />.
//     /// </summary>
//     /// <param name="thumbnailUrl"> The thumbnail URL to be set. </param>
//     /// <returns>
//     ///     The current builder.
//     /// </returns>
//     public EmbedBuilder WithThumbnailUrl(string? thumbnailUrl)
//     {
//         ThumbnailUrl = thumbnailUrl;
//         return this;
//     }
//
//     /// <summary>
//     ///     Sets the image URL of an <see cref="Embed" />.
//     /// </summary>
//     /// <param name="imageUrl">The image URL to be set.</param>
//     /// <returns>
//     ///     The current builder.
//     /// </returns>
//     public EmbedBuilder WithImageUrl(string? imageUrl)
//     {
//         ImageUrl = imageUrl;
//         return this;
//     }
//
//     /// <summary>
//     ///     Sets the timestamp of an <see cref="Embed" /> to the current time.
//     /// </summary>
//     /// <returns>
//     ///     The current builder.
//     /// </returns>
//     public EmbedBuilder WithCurrentTimestamp()
//     {
//         Timestamp = DateTimeOffset.UtcNow;
//         return this;
//     }
//
//     /// <summary>
//     ///     Sets the timestamp of an <see cref="Embed" />.
//     /// </summary>
//     /// <param name="dateTimeOffset">The timestamp to be set.</param>
//     /// <returns>
//     ///     The current builder.
//     /// </returns>
//     public EmbedBuilder WithTimestamp(DateTimeOffset? dateTimeOffset)
//     {
//         Timestamp = dateTimeOffset;
//         return this;
//     }
//
//     /// <summary>
//     ///     Sets the sidebar color of an <see cref="Embed" />.
//     /// </summary>
//     /// <param name="color">The color to be set.</param>
//     /// <returns>
//     ///     The current builder.
//     /// </returns>
//     public EmbedBuilder WithColor(Color? color)
//     {
//         Color = color;
//         return this;
//     }
//
//     /// <summary>
//     ///     Sets the <see cref="EmbedAuthorBuilder" /> of an <see cref="Embed" />.
//     /// </summary>
//     /// <param name="author">The author builder class containing the author field properties.</param>
//     /// <returns>
//     ///     The current builder.
//     /// </returns>
//     public EmbedBuilder WithAuthor(EmbedAuthorBuilder? author)
//     {
//         Author = author;
//         return this;
//     }
//
//     /// <summary>
//     ///     Sets the author field of an <see cref="Embed" /> with the provided properties.
//     /// </summary>
//     /// <param name="action">The delegate containing the author field properties.</param>
//     /// <returns>
//     ///     The current builder.
//     /// </returns>
//     public EmbedBuilder WithAuthor(Action<EmbedAuthorBuilder> action)
//     {
//         var author = new EmbedAuthorBuilder();
//         action(author);
//         Author = author;
//         return this;
//     }
//
//     /// <summary>
//     ///     Sets the author field of an <see cref="Embed" /> with the provided name, icon URL, and URL.
//     /// </summary>
//     /// <param name="name">The title of the author field.</param>
//     /// <param name="iconUrl">The icon URL of the author field.</param>
//     /// <param name="url">The URL of the author field.</param>
//     /// <returns>
//     ///     The current builder.
//     /// </returns>
//     public EmbedBuilder WithAuthor(string name, string? iconUrl = null, string? url = null)
//     {
//         var author = new EmbedAuthorBuilder {Name = name, IconUrl = iconUrl, Url = url};
//         Author = author;
//         return this;
//     }
//
//     /// <summary>
//     ///     Sets the <see cref="EmbedFooterBuilder" /> of an <see cref="Embed" />.
//     /// </summary>
//     /// <param name="footer">The footer builder class containing the footer field properties.</param>
//     /// <returns>
//     ///     The current builder.
//     /// </returns>
//     public EmbedBuilder WithFooter(EmbedFooterBuilder? footer)
//     {
//         Footer = footer;
//         return this;
//     }
//
//     /// <summary>
//     ///     Sets the footer field of an <see cref="Embed" /> with the provided properties.
//     /// </summary>
//     /// <param name="action">The delegate containing the footer field properties.</param>
//     /// <returns>
//     ///     The current builder.
//     /// </returns>
//     public EmbedBuilder WithFooter(Action<EmbedFooterBuilder> action)
//     {
//         var footer = new EmbedFooterBuilder();
//         action(footer);
//         Footer = footer;
//         return this;
//     }
//
//     /// <summary>
//     ///     Sets the footer field of an <see cref="Embed" /> with the provided name, icon URL.
//     /// </summary>
//     /// <param name="text">The title of the footer field.</param>
//     /// <param name="iconUrl">The icon URL of the footer field.</param>
//     /// <returns>
//     ///     The current builder.
//     /// </returns>
//     public EmbedBuilder WithFooter(string text, string? iconUrl = null)
//     {
//         var footer = new EmbedFooterBuilder {Text = text, IconUrl = iconUrl};
//         Footer = footer;
//         return this;
//     }
//
//     /// <summary>
//     ///     Adds an <see cref="Embed" /> field with the provided name and value.
//     /// </summary>
//     /// <param name="name">The title of the field.</param>
//     /// <param name="value">The value of the field.</param>
//     /// <param name="inline">Indicates whether the field is in-line or not.</param>
//     /// <returns>
//     ///     The current builder.
//     /// </returns>
//     public EmbedBuilder AddField(string name, object value, bool inline = false)
//     {
//         var field = new EmbedFieldBuilder()
//             .WithIsInline(inline)
//             .WithName(name)
//             .WithValue(value);
//         AddField(field);
//         return this;
//     }
//
//     /// <summary>
//     ///     Adds a field with the provided <see cref="EmbedFieldBuilder" /> to an
//     ///     <see cref="Embed" />.
//     /// </summary>
//     /// <param name="field">The field builder class containing the field properties.</param>
//     /// <exception cref="ArgumentException">Field count exceeds <see cref="MaxFieldCount" />.</exception>
//     /// <returns>
//     ///     The current builder.
//     /// </returns>
//     public EmbedBuilder AddField(EmbedFieldBuilder field)
//     {
//         if (Fields.Count >= MaxFieldCount)
//         {
//             throw new ArgumentException($"Field count must be less than or equal to {MaxFieldCount}.", nameof(field));
//         }
//
//         Fields.Add(field);
//         return this;
//     }
//
//     /// <summary>
//     ///     Adds an <see cref="Embed" /> field with the provided properties.
//     /// </summary>
//     /// <param name="action">The delegate containing the field properties.</param>
//     /// <returns>
//     ///     The current builder.
//     /// </returns>
//     public EmbedBuilder AddField(Action<EmbedFieldBuilder> action)
//     {
//         var field = new EmbedFieldBuilder();
//         action(field);
//         AddField(field);
//         return this;
//     }
//
//     /// <summary>
//     ///     Builds the <see cref="Embed" /> into a Rich Embed ready to be sent.
//     /// </summary>
//     /// <returns>
//     ///     The built embed object.
//     /// </returns>
//     /// <exception cref="InvalidOperationException">Total embed length exceeds <see cref="MaxEmbedLength" />.</exception>
//     /// <exception cref="InvalidOperationException">Any Url must include its protocols (i.e http:// or https://).</exception>
//     public Embed Build()
//     {
//         if (Length > MaxEmbedLength)
//             throw new InvalidOperationException($"Total embed length must be less than or equal to {MaxEmbedLength}.");
//         if (!string.IsNullOrEmpty(Url))
//             UrlValidation.Validate(Url, true);
//         if (!string.IsNullOrEmpty(ThumbnailUrl))
//             UrlValidation.Validate(ThumbnailUrl, true);
//         if (!string.IsNullOrEmpty(ImageUrl))
//             UrlValidation.Validate(ImageUrl, true);
//         if (Author is not null)
//         {
//             if (!string.IsNullOrEmpty(Author.Url))
//                 UrlValidation.Validate(Author.Url, true);
//             if (!string.IsNullOrEmpty(Author.IconUrl))
//                 UrlValidation.Validate(Author.IconUrl, true);
//         }
//
//         if (Footer != null)
//         {
//             if (!string.IsNullOrEmpty(Footer.IconUrl))
//                 UrlValidation.Validate(Footer.IconUrl, true);
//         }
//
//         var fields = ImmutableArray.CreateBuilder<EmbedField>(Fields.Count);
//         for (var i = 0; i < Fields.Count; i++)
//             fields.Add(Fields[i].Build());
//
//         return new Embed(EmbedType.Rich, Title, Description, Url, Timestamp, Color, _image, null, Author?.Build(),
//             Footer?.Build(), null, _thumbnail, fields.ToImmutable());
//     }
//
//     public static bool operator ==(EmbedBuilder? left, EmbedBuilder? right)
//         => left?.Equals(right) ?? right is null;
//
//     public static bool operator !=(EmbedBuilder? left, EmbedBuilder? right)
//         => !(left == right);
//
//     /// <summary>
//     ///     Determines whether the specified object is equal to the current <see cref="EmbedBuilder" />.
//     /// </summary>
//     /// <remarks>
//     ///     If the object passes is an <see cref="EmbedBuilder" />, <see cref="Equals(EmbedBuilder)" /> will be called to
//     ///     compare the 2 instances
//     /// </remarks>
//     /// <param name="obj">The object to compare with the current <see cref="EmbedBuilder" /></param>
//     /// <returns></returns>
//     public override bool Equals(object? obj)
//         => obj is EmbedBuilder embedBuilder && Equals(embedBuilder);
//
//     /// <summary>
//     ///     Determines whether the specified <see cref="EmbedBuilder" /> is equal to the current <see cref="EmbedBuilder" />
//     /// </summary>
//     /// <param name="embedBuilder">
//     ///     The <see cref="EmbedBuilder" /> to compare with the current <see cref="EmbedBuilder" />
//     /// </param>
//     /// <returns></returns>
//     public bool Equals(EmbedBuilder? embedBuilder)
//     {
//         if (embedBuilder is null)
//             return false;
//
//         if (Fields.Count != embedBuilder.Fields.Count)
//             return false;
//
//         for (var i = 0; i < _fields.Count; i++)
//             if (_fields[i] != embedBuilder._fields[i])
//                 return false;
//
//         return _title == embedBuilder?._title
//                && _description == embedBuilder?._description
//                && _image == embedBuilder?._image
//                && _thumbnail == embedBuilder?._thumbnail
//                && Timestamp == embedBuilder?.Timestamp
//                && Color == embedBuilder?.Color
//                && Author == embedBuilder?.Author
//                && Footer == embedBuilder?.Footer
//                && Url == embedBuilder?.Url;
//     }
//
//     /// <inheritdoc />
//     public override int GetHashCode() => base.GetHashCode();
// }
//
// /// <summary>
// ///     Represents a builder class for an embed field.
// /// </summary>
// public class EmbedFieldBuilder
// {
//     /// <summary>
//     ///     Gets the maximum field length for name allowed by Discord.
//     /// </summary>
//     public const int MaxFieldNameLength = 256;
//
//     /// <summary>
//     ///     Gets the maximum field length for value allowed by Discord.
//     /// </summary>
//     public const int MaxFieldValueLength = 1024;
//
//     private string _name = string.Empty;
//     private string _value = string.Empty;
//
//     /// <summary>
//     ///     Gets or sets the field name.
//     /// </summary>
//     /// <exception cref="ArgumentException">
//     ///     <para>Field name is <see langword="null" />, empty or entirely whitespace.</para>
//     ///     <para>
//     ///         <c>- or -</c>
//     ///     </para>
//     ///     <para>Field name length exceeds <see cref="MaxFieldNameLength" />.</para>
//     /// </exception>
//     /// <returns>
//     ///     The name of the field.
//     /// </returns>
//     public string Name
//     {
//         get => _name;
//         set
//         {
//             if (string.IsNullOrWhiteSpace(value))
//                 throw new ArgumentException("Field name must not be null, empty or entirely whitespace.", nameof(Name));
//             if (value.Length > MaxFieldNameLength)
//                 throw new ArgumentException($"Field name length must be less than or equal to {MaxFieldNameLength}.",
//                     nameof(Name));
//             _name = value;
//         }
//     }
//
//     /// <summary>
//     ///     Gets or sets the field value.
//     /// </summary>
//     /// <exception cref="ArgumentException" accessor="set">
//     ///     <para>Field value is <see langword="null" />, empty or entirely whitespace.</para>
//     ///     <para>
//     ///         <c>- or -</c>
//     ///     </para>
//     ///     <para>Field value length exceeds <see cref="MaxFieldValueLength" />.</para>
//     /// </exception>
//     /// <returns>
//     ///     The value of the field.
//     /// </returns>
//     public object Value
//     {
//         get => _value;
//         set
//         {
//             var stringValue = value.ToString();
//             if (string.IsNullOrWhiteSpace(stringValue))
//                 throw new ArgumentException("Field value must not be null or empty.", nameof(Value));
//             if (stringValue.Length > MaxFieldValueLength)
//                 throw new ArgumentException($"Field value length must be less than or equal to {MaxFieldValueLength}.",
//                     nameof(Value));
//             _value = stringValue;
//         }
//     }
//
//     /// <summary>
//     ///     Gets or sets a value that indicates whether the field should be in-line with each other.
//     /// </summary>
//     public bool IsInline { get; set; }
//
//     /// <summary>
//     ///     Sets the field name.
//     /// </summary>
//     /// <param name="name">The name to set the field name to.</param>
//     /// <returns>
//     ///     The current builder.
//     /// </returns>
//     public EmbedFieldBuilder WithName(string name)
//     {
//         Name = name;
//         return this;
//     }
//
//     /// <summary>
//     ///     Sets the field value.
//     /// </summary>
//     /// <param name="value">The value to set the field value to.</param>
//     /// <returns>
//     ///     The current builder.
//     /// </returns>
//     public EmbedFieldBuilder WithValue(object value)
//     {
//         Value = value;
//         return this;
//     }
//
//     /// <summary>
//     ///     Determines whether the field should be in-line with each other.
//     /// </summary>
//     /// <returns>
//     ///     The current builder.
//     /// </returns>
//     public EmbedFieldBuilder WithIsInline(bool isInline)
//     {
//         IsInline = isInline;
//         return this;
//     }
//
//     /// <summary>
//     ///     Builds the field builder into a <see cref="EmbedField" /> class.
//     /// </summary>
//     /// <returns>
//     ///     The current builder.
//     /// </returns>
//     /// <exception cref="ArgumentException">
//     ///     <para><see cref="Name" /> or <see cref="Value" /> is <see langword="null" />, empty or entirely whitespace.</para>
//     ///     <para>
//     ///         <c>- or -</c>
//     ///     </para>
//     ///     <para><see cref="Name" /> or <see cref="Value" /> exceeds the maximum length allowed by Discord.</para>
//     /// </exception>
//     public EmbedField Build() => new(Name, Value.ToString()!, IsInline);
//
//     public static bool operator ==(EmbedFieldBuilder? left, EmbedFieldBuilder? right)
//         => left?.Equals(right) ?? right is null;
//
//     public static bool operator !=(EmbedFieldBuilder? left, EmbedFieldBuilder? right)
//         => !(left == right);
//
//     /// <summary>
//     ///     Determines whether the specified object is equal to the current <see cref="EmbedFieldBuilder" />.
//     /// </summary>
//     /// <remarks>
//     ///     If the object passes is an <see cref="EmbedFieldBuilder" />, <see cref="Equals(EmbedFieldBuilder)" /> will be
//     ///     called to compare the 2 instances
//     /// </remarks>
//     /// <param name="obj">The object to compare with the current <see cref="EmbedFieldBuilder" /></param>
//     /// <returns></returns>
//     public override bool Equals(object? obj)
//         => obj is EmbedFieldBuilder embedFieldBuilder && Equals(embedFieldBuilder);
//
//     /// <summary>
//     ///     Determines whether the specified <see cref="EmbedFieldBuilder" /> is equal to the current
//     ///     <see cref="EmbedFieldBuilder" />
//     /// </summary>
//     /// <param name="embedFieldBuilder">
//     ///     The <see cref="EmbedFieldBuilder" /> to compare with the current
//     ///     <see cref="EmbedFieldBuilder" />
//     /// </param>
//     /// <returns></returns>
//     public bool Equals(EmbedFieldBuilder? embedFieldBuilder)
//         => _name == embedFieldBuilder?._name
//            && _value == embedFieldBuilder?._value
//            && IsInline == embedFieldBuilder?.IsInline;
//
//     /// <inheritdoc />
//     public override int GetHashCode() => base.GetHashCode();
// }
//
// /// <summary>
// ///     Represents a builder class for a author field.
// /// </summary>
// public class EmbedAuthorBuilder
// {
//     /// <summary>
//     ///     Gets the maximum author name length allowed by Discord.
//     /// </summary>
//     public const int MaxAuthorNameLength = 256;
//
//     private string _name = string.Empty;
//
//     /// <summary>
//     ///     Gets or sets the author name.
//     /// </summary>
//     /// <exception cref="ArgumentException">
//     ///     Author name length is longer than <see cref="MaxAuthorNameLength" />.
//     /// </exception>
//     /// <returns>
//     ///     The author name.
//     /// </returns>
//     public string Name
//     {
//         get => _name;
//         set
//         {
//             if (value?.Length > MaxAuthorNameLength)
//                 throw new ArgumentException($"Author name length must be less than or equal to {MaxAuthorNameLength}.",
//                     nameof(Name));
//             _name = value ?? throw new ArgumentNullException(nameof(value), "Author name must have a non-null value.");
//         }
//     }
//
//     /// <summary>
//     ///     Gets or sets the URL of the author field.
//     /// </summary>
//     /// <exception cref="ArgumentException" accessor="set">Url is not a well-formed <see cref="Uri" />.</exception>
//     /// <returns>
//     ///     The URL of the author field.
//     /// </returns>
//     public string? Url { get; set; }
//
//     /// <summary>
//     ///     Gets or sets the icon URL of the author field.
//     /// </summary>
//     /// <exception cref="ArgumentException" accessor="set">Url is not a well-formed <see cref="Uri" />.</exception>
//     /// <returns>
//     ///     The icon URL of the author field.
//     /// </returns>
//     public string? IconUrl { get; set; }
//
//     /// <summary>
//     ///     Sets the name of the author field.
//     /// </summary>
//     /// <param name="name">The name of the author field.</param>
//     /// <returns>
//     ///     The current builder.
//     /// </returns>
//     public EmbedAuthorBuilder WithName(string name)
//     {
//         Name = name;
//         return this;
//     }
//
//     /// <summary>
//     ///     Sets the URL of the author field.
//     /// </summary>
//     /// <param name="url">The URL of the author field.</param>
//     /// <returns>
//     ///     The current builder.
//     /// </returns>
//     public EmbedAuthorBuilder WithUrl(string? url)
//     {
//         Url = url;
//         return this;
//     }
//
//     /// <summary>
//     ///     Sets the icon URL of the author field.
//     /// </summary>
//     /// <param name="iconUrl">The icon URL of the author field.</param>
//     /// <returns>
//     ///     The current builder.
//     /// </returns>
//     public EmbedAuthorBuilder WithIconUrl(string? iconUrl)
//     {
//         IconUrl = iconUrl;
//         return this;
//     }
//
//     /// <summary>
//     ///     Builds the author field to be used.
//     /// </summary>
//     /// <exception cref="ArgumentException">
//     ///     <para>Author name length is longer than <see cref="MaxAuthorNameLength" />.</para>
//     ///     <para>
//     ///         <c>- or -</c>
//     ///     </para>
//     ///     <para><see cref="Url" /> is not a well-formed <see cref="Uri" />.</para>
//     ///     <para>
//     ///         <c>- or -</c>
//     ///     </para>
//     ///     <para><see cref="IconUrl" /> is not a well-formed <see cref="Uri" />.</para>
//     /// </exception>
//     /// <returns>
//     ///     The built author field.
//     /// </returns>
//     public EmbedAuthor Build()
//         => new(Name, Url, IconUrl, null);
//
//     public static bool operator ==(EmbedAuthorBuilder? left, EmbedAuthorBuilder? right)
//         => left?.Equals(right) ?? right is null;
//
//     public static bool operator !=(EmbedAuthorBuilder left, EmbedAuthorBuilder right)
//         => !(left == right);
//
//     /// <summary>
//     ///     Determines whether the specified object is equal to the current <see cref="EmbedAuthorBuilder" />.
//     /// </summary>
//     /// <remarks>
//     ///     If the object passes is an <see cref="EmbedAuthorBuilder" />, <see cref="Equals(EmbedAuthorBuilder)" /> will be
//     ///     called to compare the 2 instances
//     /// </remarks>
//     /// <param name="obj">The object to compare with the current <see cref="EmbedAuthorBuilder" /></param>
//     /// <returns></returns>
//     public override bool Equals(object? obj)
//         => obj is EmbedAuthorBuilder embedAuthorBuilder && Equals(embedAuthorBuilder);
//
//     /// <summary>
//     ///     Determines whether the specified <see cref="EmbedAuthorBuilder" /> is equals to the current
//     ///     <see cref="EmbedAuthorBuilder" />
//     /// </summary>
//     /// <param name="embedAuthorBuilder">
//     ///     The <see cref="EmbedAuthorBuilder" /> to compare with the current
//     ///     <see cref="EmbedAuthorBuilder" />
//     /// </param>
//     /// <returns></returns>
//     public bool Equals(EmbedAuthorBuilder? embedAuthorBuilder)
//         => _name == embedAuthorBuilder?._name
//            && Url == embedAuthorBuilder?.Url
//            && IconUrl == embedAuthorBuilder?.IconUrl;
//
//     /// <inheritdoc />
//     public override int GetHashCode() => base.GetHashCode();
// }
//
// /// <summary>
// ///     Represents a builder class for an embed footer.
// /// </summary>
// public class EmbedFooterBuilder
// {
//     /// <summary>
//     ///     Gets the maximum footer length allowed by Discord.
//     /// </summary>
//     public const int MaxFooterTextLength = 2048;
//
//     private string _text = string.Empty;
//
//     /// <summary>
//     ///     Gets or sets the footer text.
//     /// </summary>
//     /// <exception cref="ArgumentException">
//     ///     Author name length is longer than <see cref="MaxFooterTextLength" />.
//     /// </exception>
//     /// <returns>
//     ///     The footer text.
//     /// </returns>
//     public string Text
//     {
//         get => _text;
//         set
//         {
//             if (value?.Length > MaxFooterTextLength)
//                 throw new ArgumentException($"Footer text length must be less than or equal to {MaxFooterTextLength}.",
//                     nameof(Text));
//             _text = value ?? throw new ArgumentNullException(nameof(value), "Footer text must have a non-null value.");
//         }
//     }
//
//     /// <summary>
//     ///     Gets or sets the icon URL of the footer field.
//     /// </summary>
//     /// <exception cref="ArgumentException" accessor="set">Url is not a well-formed <see cref="Uri" />.</exception>
//     /// <returns>
//     ///     The icon URL of the footer field.
//     /// </returns>
//     public string? IconUrl { get; set; }
//
//     /// <summary>
//     ///     Sets the name of the footer field.
//     /// </summary>
//     /// <param name="text">The text of the footer field.</param>
//     /// <returns>
//     ///     The current builder.
//     /// </returns>
//     public EmbedFooterBuilder WithText(string text)
//     {
//         Text = text;
//         return this;
//     }
//
//     /// <summary>
//     ///     Sets the icon URL of the footer field.
//     /// </summary>
//     /// <param name="iconUrl">The icon URL of the footer field.</param>
//     /// <returns>
//     ///     The current builder.
//     /// </returns>
//     public EmbedFooterBuilder WithIconUrl(string? iconUrl)
//     {
//         IconUrl = iconUrl;
//         return this;
//     }
//
//     /// <summary>
//     ///     Builds the footer field to be used.
//     /// </summary>
//     /// <returns></returns>
//     /// <exception cref="ArgumentException">
//     ///     <para><see cref="Text" /> length is longer than <see cref="MaxFooterTextLength" />.</para>
//     ///     <para>
//     ///         <c>- or -</c>
//     ///     </para>
//     ///     <para><see cref="IconUrl" /> is not a well-formed <see cref="Uri" />.</para>
//     /// </exception>
//     /// <returns>
//     ///     A built footer field.
//     /// </returns>
//     public EmbedFooter Build()
//         => new(Text, IconUrl, null);
//
//     public static bool operator ==(EmbedFooterBuilder? left, EmbedFooterBuilder? right)
//         => left?.Equals(right) ?? right is null;
//
//     public static bool operator !=(EmbedFooterBuilder? left, EmbedFooterBuilder? right)
//         => !(left == right);
//
//     /// <summary>
//     ///     Determines whether the specified object is equal to the current <see cref="EmbedFooterBuilder" />.
//     /// </summary>
//     /// <remarks>
//     ///     If the object passes is an <see cref="EmbedFooterBuilder" />, <see cref="Equals(EmbedFooterBuilder)" /> will be
//     ///     called to compare the 2 instances
//     /// </remarks>
//     /// <param name="obj">The object to compare with the current <see cref="EmbedFooterBuilder" /></param>
//     /// <returns></returns>
//     public override bool Equals(object? obj)
//         => obj is EmbedFooterBuilder embedFooterBuilder && Equals(embedFooterBuilder);
//
//     /// <summary>
//     ///     Determines whether the specified <see cref="EmbedFooterBuilder" /> is equal to the current
//     ///     <see cref="EmbedFooterBuilder" />
//     /// </summary>
//     /// <param name="embedFooterBuilder">
//     ///     The <see cref="EmbedFooterBuilder" /> to compare with the current
//     ///     <see cref="EmbedFooterBuilder" />
//     /// </param>
//     /// <returns></returns>
//     public bool Equals(EmbedFooterBuilder? embedFooterBuilder)
//         => _text == embedFooterBuilder?._text
//            && IconUrl == embedFooterBuilder?.IconUrl;
//
//     /// <inheritdoc />
//     public override int GetHashCode() => base.GetHashCode();
// }


