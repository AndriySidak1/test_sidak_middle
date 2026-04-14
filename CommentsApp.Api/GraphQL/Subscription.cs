using HotChocolate.Execution;
using HotChocolate.Subscriptions;

namespace CommentsApp.Api.GraphQL;

public sealed class Subscription
{
    [Subscribe]
    [Topic]
    public CommentGqlDto OnCommentCreated([EventMessage] CommentGqlDto comment) => comment;
}
