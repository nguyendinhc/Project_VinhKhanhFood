const vkSwal = Swal.mixin({
const vkSwal = Swal.mixin({
    customClass: {
        popup: 'vk-swal-popup',
        title: 'vk-swal-title',
        htmlContainer: 'vk-swal-text',
        confirmButton: 'vk-swal-confirm',
        cancelButton: 'vk-swal-cancel'
    },
    buttonsStyling: false
});

window.confirmDeletePoi = async function (poiName) {
    const result = await vkSwal.fire({
        title: 'Xác nhận xóa?',
        text: `Bạn có chắc muốn xóa địa điểm "${poiName}" không?`,
        icon: 'warning',
        showCancelButton: true,
        confirmButtonText: 'Xóa ngay',
        cancelButtonText: 'Hủy',
        reverseButtons: true,
        focusCancel: true
    });

    return result.isConfirmed;
};

window.confirmDeleteUser = async function (userName) {
    const result = await vkSwal.fire({
        title: 'Xác nhận xóa?',
        text: `Bạn có chắc muốn xóa user "${userName}" không?`,
        icon: 'warning',
        showCancelButton: true,
        confirmButtonText: 'Xóa ngay',
        cancelButtonText: 'Hủy',
        reverseButtons: true,
        focusCancel: true
    });

    return result.isConfirmed;
};

window.showSuccessMessage = async function (title, text) {
    await vkSwal.fire({
        title,
        text,
        icon: 'success',
        confirmButtonText: 'Đã hiểu'
    });
};

window.showErrorMessage = async function (title, text) {
    await vkSwal.fire({
        title,
        text,
        icon: 'error',
        confirmButtonText: 'Đóng'
    });
};
