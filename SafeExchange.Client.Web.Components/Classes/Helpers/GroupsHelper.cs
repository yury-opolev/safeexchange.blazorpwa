// GroupsHelper

namespace SafeExchange.Client.Web.Components
{
    using SafeExchange.Client.Common;
    using SafeExchange.Client.Common.Model;
    using System;
    using System.Threading.Tasks;

    public static class GroupsHelper
    {
        public static async Task<BaseResponseObject<GraphGroupOutput>> SwitchGroupPinAsync(ApiClient apiClient, StateContainer stateContainer, GraphGroupOutput item, bool newPinValue)
        {
            var resultResponse = new BaseResponseObject<GraphGroupOutput>()
            {
                Status = string.Empty,
                SubStatus = string.Empty,
                Result = new GraphGroupOutput()
                {
                    Id = item.Id,
                    DisplayName = item.DisplayName,
                    Mail = item.Mail
                }
            };

            if (newPinValue)
            {
                BaseResponseObject<PinnedGroupOutput> pinnedGroupResponse = default;
                try
                {
                    pinnedGroupResponse = await apiClient.PutPinnedGroupAsync(item.Id, new PinnedGroupInput()
                    {
                        GroupId = item.Id,
                        GroupDisplayName = item.DisplayName,
                        GroupMail = item.Mail
                    });
                }
                catch (Exception ex)
                {
                    pinnedGroupResponse = new BaseResponseObject<PinnedGroupOutput>()
                    {
                        Status = "error",
                        SubStatus = "error",
                        Error = $"{ex.GetType()}: {ex.Message ?? "Unknown exception."}"
                    };
                }

                resultResponse.Status = pinnedGroupResponse.Status;
                resultResponse.SubStatus = pinnedGroupResponse.SubStatus;
                resultResponse.Error = pinnedGroupResponse.Error;

                var requestSucceeded = pinnedGroupResponse.Status == "ok";
                if (requestSucceeded && pinnedGroupResponse.Result != default)
                {
                    resultResponse.Result.Id = pinnedGroupResponse.Result.GroupId;
                    resultResponse.Result.DisplayName = pinnedGroupResponse.Result.GroupDisplayName;
                    resultResponse.Result.Mail = pinnedGroupResponse.Result.GroupMail;
                }

                if (requestSucceeded)
                {
                    var pinnedGroupIndex = stateContainer.PinnedGroups.FindIndex(x => x.GroupId.Equals(item.Id));
                    if (pinnedGroupIndex == -1)
                    {
                        stateContainer.PinnedGroups.Add(new PinnedGroup(item.Id, item.DisplayName, item.Mail));
                    }
                }
            }
            else
            {
                BaseResponseObject<string> pinnedGroupDeletionResponse = default;
                try
                {
                    pinnedGroupDeletionResponse = await apiClient.DeletePinnedGroupAsync(item.Id);
                }
                catch (Exception ex)
                {
                    pinnedGroupDeletionResponse = new BaseResponseObject<string>()
                    {
                        Status = "error",
                        SubStatus = "error",
                        Error = $"{ex.GetType()}: {ex.Message ?? "Unknown exception."}"
                    };
                }

                resultResponse.Status = pinnedGroupDeletionResponse.Status;
                resultResponse.SubStatus = pinnedGroupDeletionResponse.SubStatus;
                resultResponse.Error = pinnedGroupDeletionResponse.Error;

                var requestSucceeded = pinnedGroupDeletionResponse.Status == "ok";
                if (requestSucceeded)
                {
                    var pinnedGroupIndex = stateContainer.PinnedGroups.FindIndex(x => x.GroupId.Equals(item.Id));
                    if (pinnedGroupIndex >= 0)
                    {
                        stateContainer.PinnedGroups.RemoveAt(pinnedGroupIndex);
                    }
                }
            }

            return resultResponse;
        }
    }
}
