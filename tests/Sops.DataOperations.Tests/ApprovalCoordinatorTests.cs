using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Sops.DataOperations.Abstractions;
using Sops.DataOperations.Abstractions.Models;
using Sops.DataOperations.Approvals;
using Sops.DataOperations.Security;
using Sops.DataOperations.Sync.Storage;
using Sops.Models.Sync;
using Xunit;

namespace Sops.DataOperations.Tests
{
    public class ApprovalCoordinatorTests
    {
        private readonly ChangeApprovalCoordinator _coordinator;
        private readonly InMemorySyncStore _outbox;
        private readonly ChangeRequestDispatcher _dispatcher;
        private readonly ApprovedChangeApplicator _applicator;
        private readonly ISyncTransport _transport;

        public ApprovalCoordinatorTests()
        {
            _outbox = new InMemorySyncStore();
            _transport = Substitute.For<ISyncTransport>();
            _transport.CheckGlobalConnectivityAsync(Arg.Any<CancellationToken>()).Returns(Task.FromResult(false)); // offline queueing

            _dispatcher = new ChangeRequestDispatcher(_outbox, _transport);
            _applicator = new ApprovedChangeApplicator();
            var decisionHandler = new ApprovalDecisionHandler();

            _coordinator = new ChangeApprovalCoordinator(
                decisionHandler,
                _dispatcher,
                _applicator,
                NullLogger<ChangeApprovalCoordinator>.Instance);
        }

        [Fact]
        public async Task SubmitRequestAsync_ValidSubmission_ReturnsPendingRequestAndEnqueuesOutbox()
        {
            var requesterId = Guid.NewGuid();
            var submission = new ChangeRequestSubmission
            {
                PharmacyId = Guid.NewGuid(),
                ChainId = Guid.NewGuid(),
                RequestedByUserId = requesterId,
                EntityType = "DrugPrice",
                ChangeType = "PRICE_OVERRIDE",
                Reason = "Correction of wholesale pricing tier"
            };

            var result = await _coordinator.SubmitRequestAsync(submission);

            Assert.True(result.Success);
            Assert.NotNull(result.Data);
            Assert.Equal(ChangeRequestStatus.Pending, result.Data.Status);
            Assert.Equal("PRICE_OVERRIDE", result.Data.ChangeType);

            // Verify outbox queued the change request event
            var pendingEvents = await _outbox.GetPendingAsync(10);
            Assert.Contains(pendingEvents, e => e.Envelope.AggregateType == "ChangeRequest" && e.Envelope.AggregateId == result.Data.RequestId.ToString());
        }

        [Fact]
        public async Task SubmitRequestAsync_MissingEntityTypeOrReason_FailsValidation()
        {
            var submission = new ChangeRequestSubmission
            {
                PharmacyId = Guid.NewGuid(),
                RequestedByUserId = Guid.NewGuid(),
                EntityType = "",
                Reason = "Reason without entity"
            };

            var result = await _coordinator.SubmitRequestAsync(submission);
            Assert.False(result.Success);
            Assert.Equal("INVALID_SUBMISSION", result.ErrorCode);
        }

        [Fact]
        public async Task ReviewRequestAsync_AuthorizedReviewer_ApprovesRequest()
        {
            var requesterId = Guid.NewGuid();
            var submitResult = await _coordinator.SubmitRequestAsync(new ChangeRequestSubmission
            {
                PharmacyId = Guid.NewGuid(),
                RequestedByUserId = requesterId,
                EntityType = "InventoryItem",
                Reason = "Damaged stock write-off"
            });

            var reviewer = new UserIdentity
            {
                UserId = Guid.NewGuid(), // Distinct user
                Username = "pharmacy_manager",
                Permissions = new List<string> { Permissions.ApprovalApprove }
            };

            var decision = new ChangeDecision
            {
                Decision = "Approved",
                DecidedByUserId = reviewer.UserId,
                Notes = "Stock write-off confirmed by inspection."
            };

            var reviewResult = await _coordinator.ReviewRequestAsync(submitResult.Data!.RequestId, decision, reviewer);

            Assert.True(reviewResult.Success);
            Assert.Equal(ChangeRequestStatus.Approved, reviewResult.Data!.NewStatus);

            var updatedRequest = await _coordinator.GetRequestByIdAsync(submitResult.Data.RequestId);
            Assert.NotNull(updatedRequest);
            Assert.Equal(ChangeRequestStatus.Approved, updatedRequest.Status);
            Assert.Equal(reviewer.UserId, updatedRequest.Decision!.DecidedByUserId);
        }

        [Fact]
        public async Task ReviewRequestAsync_SeparationOfDutiesViolation_FailsReview()
        {
            var requesterId = Guid.NewGuid();
            var submitResult = await _coordinator.SubmitRequestAsync(new ChangeRequestSubmission
            {
                PharmacyId = Guid.NewGuid(),
                RequestedByUserId = requesterId,
                EntityType = "NarcoticsSchedule",
                Reason = "Adjust safety threshold"
            });

            var sameUserAsReviewer = new UserIdentity
            {
                UserId = requesterId, // Same as requester!
                Username = "pharmacist_attempting_self_approval",
                Permissions = new List<string> { Permissions.ApprovalApprove }
            };

            var decision = new ChangeDecision
            {
                Decision = "Approved",
                DecidedByUserId = requesterId,
                Notes = "Self approval attempt"
            };

            var reviewResult = await _coordinator.ReviewRequestAsync(submitResult.Data!.RequestId, decision, sameUserAsReviewer);

            Assert.False(reviewResult.Success);
            Assert.Equal("REVIEW_FAILED", reviewResult.ErrorCode);
            Assert.Contains("Separation of duties violation", reviewResult.ErrorMessage);
        }

        [Fact]
        public async Task ReviewRequestAsync_MissingPermission_FailsReview()
        {
            var submitResult = await _coordinator.SubmitRequestAsync(new ChangeRequestSubmission
            {
                PharmacyId = Guid.NewGuid(),
                RequestedByUserId = Guid.NewGuid(),
                EntityType = "DiscountRule",
                Reason = "Staff discount bump"
            });

            var unprivilegedUser = new UserIdentity
            {
                UserId = Guid.NewGuid(),
                Username = "cashier_junior",
                Permissions = new List<string> { "SomeOtherPermission" }
            };

            var decision = new ChangeDecision
            {
                Decision = "Approved",
                DecidedByUserId = unprivilegedUser.UserId
            };

            var reviewResult = await _coordinator.ReviewRequestAsync(submitResult.Data!.RequestId, decision, unprivilegedUser);

            Assert.False(reviewResult.Success);
            Assert.Equal("REVIEW_FAILED", reviewResult.ErrorCode);
        }

        [Fact]
        public async Task ApplyApprovedChangeAsync_ExecutesAndIncrementsRevision()
        {
            var submitResult = await _coordinator.SubmitRequestAsync(new ChangeRequestSubmission
            {
                PharmacyId = Guid.NewGuid(),
                RequestedByUserId = Guid.NewGuid(),
                EntityType = "DrugBatch",
                Reason = "Expiry date correction"
            });

            var reviewer = new UserIdentity
            {
                UserId = Guid.NewGuid(),
                Username = "senior_admin",
                Permissions = new List<string> { Permissions.ApprovalApprove }
            };

            await _coordinator.ReviewRequestAsync(submitResult.Data!.RequestId, new ChangeDecision
            {
                Decision = "Approved",
                DecidedByUserId = reviewer.UserId
            }, reviewer);

            var applyResult = await _coordinator.ApplyApprovedChangeAsync(submitResult.Data.RequestId);

            Assert.True(applyResult.Success);
            Assert.True(applyResult.Data!.Success);
            Assert.True(applyResult.Data.NewCanonicalRevision > 0);
        }

        [Fact]
        public async Task GetPendingRequestsAsync_FiltersByBranchId()
        {
            var branchA = Guid.NewGuid();
            var branchB = Guid.NewGuid();

            await _coordinator.SubmitRequestAsync(new ChangeRequestSubmission
            {
                PharmacyId = branchA,
                RequestedByUserId = Guid.NewGuid(),
                EntityType = "T1",
                Reason = "Branch A request"
            });
            await _coordinator.SubmitRequestAsync(new ChangeRequestSubmission
            {
                PharmacyId = branchB,
                RequestedByUserId = Guid.NewGuid(),
                EntityType = "T2",
                Reason = "Branch B request"
            });

            var pendingA = await _coordinator.GetPendingRequestsAsync(branchA);
            Assert.Single(pendingA);
            Assert.Equal(branchA, pendingA[0].BranchId);

            var allPending = await _coordinator.GetPendingRequestsAsync();
            Assert.Equal(2, allPending.Count);
        }
    }
}
