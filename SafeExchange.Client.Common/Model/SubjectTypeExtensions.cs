///
/// SubjectTypeExtensions
///

namespace SafeExchange.Client.Common.Model
{
    using System;

    public static class SubjectTypeExtensions
    {
        public static SubjectType ToSubjectType(this SubjectTypeOutput output)
        {
            switch (output)
            {
                case SubjectTypeOutput.User:
                    return SubjectType.User;

                case SubjectTypeOutput.Group:
                    return SubjectType.Group;

                case SubjectTypeOutput.Application:
                    return SubjectType.Application;

                default:
                    throw new ArgumentException($"Cannot convert '{output}' to {nameof(SubjectType)}.");
            }
        }

        public static SubjectTypeInput ToDto(this SubjectType output)
        {
            switch (output)
            {
                case SubjectType.User:
                    return SubjectTypeInput.User;

                case SubjectType.Group:
                    return SubjectTypeInput.Group;

                case SubjectType.Application:
                    return SubjectTypeInput.Application;

                default:
                    throw new ArgumentException($"Cannot convert '{output}' to {nameof(SubjectType)}.");
            }
        }

    }
}
